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
    [Route("api/opportunities")]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;

        public OpportunitiesApiController(ApplicationDbContext context, IAuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // GET: api/OpportunitiesApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities([FromQuery] string? search, [FromQuery] OpportunityStage? stage)
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(o => o.Title.ToLower().Contains(term) || (o.Customer != null && o.Customer.Name.ToLower().Contains(term)));
            }

            if (stage.HasValue)
            {
                query = query.Where(o => o.Stage == stage.Value);
            }

            var list = await query.Select(o => new OpportunityDto
            {
                Id = o.Id,
                Title = o.Title,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.Name : null,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Notes = o.Notes,
                AssignedToUserId = o.AssignedToUserId,
                AssignedToUserName = o.AssignedToUser != null ? o.AssignedToUser.FullName : null,
                CreatedAt = o.CreatedAt
            }).ToListAsync();

            return Ok(list);
        }

        // GET: api/OpportunitiesApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OpportunityDto>> GetOpportunity(int id)
        {
            var o = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (o == null)
            {
                return NotFound(new { message = $"Opportunity with ID {id} not found." });
            }

            var dto = new OpportunityDto
            {
                Id = o.Id,
                Title = o.Title,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.Name,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Notes = o.Notes,
                AssignedToUserId = o.AssignedToUserId,
                AssignedToUserName = o.AssignedToUser?.FullName,
                CreatedAt = o.CreatedAt
            };

            return Ok(dto);
        }

        // POST: api/OpportunitiesApi
        [HttpPost]
        public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] OpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _context.Customers.AnyAsync(c => c.Id == dto.CustomerId))
            {
                return BadRequest(new { message = "Invalid Customer ID." });
            }

            var opp = new Opportunity
            {
                Title = dto.Title,
                CustomerId = dto.CustomerId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Notes = dto.Notes,
                AssignedToUserId = dto.AssignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Opportunity", opp.Id.ToString(), "ApiCreate",
                $"API created opportunity '{opp.Title}' worth {opp.Amount:C0}.");

            dto.Id = opp.Id;
            dto.CreatedAt = opp.CreatedAt;

            return CreatedAtAction(nameof(GetOpportunity), new { id = opp.Id }, dto);
        }

        // PUT: api/OpportunitiesApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] OpportunityDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch." });
            }

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null)
            {
                return NotFound(new { message = $"Opportunity with ID {id} not found." });
            }

            opp.Title = dto.Title;
            opp.CustomerId = dto.CustomerId;
            opp.Amount = dto.Amount;
            opp.Stage = dto.Stage;
            opp.Probability = dto.Probability;
            opp.ExpectedCloseDate = dto.ExpectedCloseDate;
            opp.Notes = dto.Notes;
            opp.AssignedToUserId = dto.AssignedToUserId;
            opp.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Opportunity", id.ToString(), "ApiUpdate",
                $"API updated opportunity '{opp.Title}'.");

            return NoContent();
        }

        // DELETE: api/OpportunitiesApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null)
            {
                return NotFound(new { message = $"Opportunity with ID {id} not found." });
            }

            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Opportunity", id.ToString(), "ApiDelete",
                $"API deleted opportunity '{opp.Title}'.");

            return NoContent();
        }
    }
}
