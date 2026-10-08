using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Models.Dtos;
using AcxiomCrm.Services;

namespace AcxiomCrm.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/leads")]
    public class LeadsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;

        public LeadsApiController(ApplicationDbContext context, IAuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // GET: api/LeadsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads([FromQuery] string? search, [FromQuery] LeadStatus? status)
        {
            var query = _context.Leads.Include(l => l.AssignedToUser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => l.FirstName.ToLower().Contains(term) || l.LastName.ToLower().Contains(term) || l.Email.ToLower().Contains(term));
            }

            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            var list = await query.Select(l => new LeadDto
            {
                Id = l.Id,
                FirstName = l.FirstName,
                LastName = l.LastName,
                Email = l.Email,
                Phone = l.Phone,
                Company = l.Company,
                Source = l.Source,
                Status = l.Status,
                Notes = l.Notes,
                EstimatedValue = l.EstimatedValue,
                AssignedToUserId = l.AssignedToUserId,
                AssignedToUserName = l.AssignedToUser != null ? l.AssignedToUser.FullName : null,
                ConvertedCustomerId = l.ConvertedCustomerId,
                CreatedAt = l.CreatedAt
            }).ToListAsync();

            return Ok(list);
        }

        // GET: api/LeadsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<LeadDto>> GetLead(int id)
        {
            var l = await _context.Leads
                .Include(l => l.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (l == null)
            {
                return NotFound(new { message = $"Lead with ID {id} not found." });
            }

            var dto = new LeadDto
            {
                Id = l.Id,
                FirstName = l.FirstName,
                LastName = l.LastName,
                Email = l.Email,
                Phone = l.Phone,
                Company = l.Company,
                Source = l.Source,
                Status = l.Status,
                Notes = l.Notes,
                EstimatedValue = l.EstimatedValue,
                AssignedToUserId = l.AssignedToUserId,
                AssignedToUserName = l.AssignedToUser?.FullName,
                ConvertedCustomerId = l.ConvertedCustomerId,
                CreatedAt = l.CreatedAt
            };

            return Ok(dto);
        }

        // POST: api/LeadsApi
        [HttpPost]
        public async Task<ActionResult<LeadDto>> CreateLead([FromBody] LeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lead = new Lead
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                Company = dto.Company,
                Source = dto.Source,
                Status = dto.Status,
                Notes = dto.Notes,
                EstimatedValue = dto.EstimatedValue,
                AssignedToUserId = dto.AssignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Lead", lead.Id.ToString(), "ApiCreate",
                $"API created lead '{lead.FullName}'.");

            dto.Id = lead.Id;
            dto.CreatedAt = lead.CreatedAt;

            return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, dto);
        }

        // PUT: api/LeadsApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLead(int id, [FromBody] LeadDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch." });
            }

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return NotFound(new { message = $"Lead with ID {id} not found." });
            }

            lead.FirstName = dto.FirstName;
            lead.LastName = dto.LastName;
            lead.Email = dto.Email;
            lead.Phone = dto.Phone;
            lead.Company = dto.Company;
            lead.Source = dto.Source;
            lead.Status = dto.Status;
            lead.Notes = dto.Notes;
            lead.EstimatedValue = dto.EstimatedValue;
            lead.AssignedToUserId = dto.AssignedToUserId;

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Lead", id.ToString(), "ApiUpdate",
                $"API updated lead '{lead.FullName}'.");

            return NoContent();
        }

        // POST: api/LeadsApi/5/convert
        [HttpPost("{id}/convert")]
        public async Task<IActionResult> ConvertLead(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return NotFound(new { message = $"Lead with ID {id} not found." });
            }

            if (lead.Status == LeadStatus.Converted)
            {
                return BadRequest(new { message = "Lead has already been converted." });
            }

            var customer = new Customer
            {
                Name = !string.IsNullOrEmpty(lead.Company) ? lead.Company : lead.FullName,
                Email = lead.Email,
                Phone = lead.Phone,
                Company = lead.Company,
                Status = CustomerStatus.Active,
                AssignedToUserId = lead.AssignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.Id;
            lead.ConvertedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Lead", lead.Id.ToString(), "ApiConvert",
                $"API converted lead '{lead.FullName}' to customer '{customer.Name}'.");

            return Ok(new { customerId = customer.Id, message = "Lead converted successfully." });
        }

        // DELETE: api/LeadsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLead(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return NotFound(new { message = $"Lead with ID {id} not found." });
            }

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Lead", id.ToString(), "ApiDelete",
                $"API deleted lead '{lead.FullName}'.");

            return NoContent();
        }
    }
}
