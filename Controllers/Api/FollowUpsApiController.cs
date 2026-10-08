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
    public class FollowUpsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;

        public FollowUpsApiController(ApplicationDbContext context, IAuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // GET: api/FollowUpsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps([FromQuery] FollowUpStatus? status, [FromQuery] int? customerId)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(f => f.Status == status.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(f => f.CustomerId == customerId.Value);
            }

            var list = await query.Select(f => new FollowUpDto
            {
                Id = f.Id,
                Subject = f.Subject,
                Type = f.Type,
                DueDate = f.DueDate,
                Status = f.Status,
                Priority = f.Priority,
                Notes = f.Notes,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.Name : null,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? (f.Lead.FirstName + " " + f.Lead.LastName) : null,
                OpportunityId = f.OpportunityId,
                OpportunityTitle = f.Opportunity != null ? f.Opportunity.Title : null,
                AssignedToUserId = f.AssignedToUserId,
                AssignedToUserName = f.AssignedToUser != null ? f.AssignedToUser.FullName : null,
                CreatedAt = f.CreatedAt,
                CompletedAt = f.CompletedAt
            }).ToListAsync();

            return Ok(list);
        }

        // GET: api/FollowUpsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<FollowUpDto>> GetFollowUp(int id)
        {
            var f = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (f == null)
            {
                return NotFound(new { message = $"Task with ID {id} not found." });
            }

            var dto = new FollowUpDto
            {
                Id = f.Id,
                Subject = f.Subject,
                Type = f.Type,
                DueDate = f.DueDate,
                Status = f.Status,
                Priority = f.Priority,
                Notes = f.Notes,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer?.Name,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? (f.Lead.FirstName + " " + f.Lead.LastName) : null,
                OpportunityId = f.OpportunityId,
                OpportunityTitle = f.Opportunity?.Title,
                AssignedToUserId = f.AssignedToUserId,
                AssignedToUserName = f.AssignedToUser?.FullName,
                CreatedAt = f.CreatedAt,
                CompletedAt = f.CompletedAt
            };

            return Ok(dto);
        }

        // POST: api/FollowUpsApi
        [HttpPost]
        public async Task<ActionResult<FollowUpDto>> CreateFollowUp([FromBody] FollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var followUp = new FollowUp
            {
                Subject = dto.Subject,
                Type = dto.Type,
                DueDate = dto.DueDate,
                Status = dto.Status,
                Priority = dto.Priority,
                Notes = dto.Notes,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                AssignedToUserId = dto.AssignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("FollowUp", followUp.Id.ToString(), "ApiCreate",
                $"API created task '{followUp.Subject}'.");

            dto.Id = followUp.Id;
            dto.CreatedAt = followUp.CreatedAt;

            return CreatedAtAction(nameof(GetFollowUp), new { id = followUp.Id }, dto);
        }

        // POST: api/FollowUpsApi/5/toggle-complete
        [HttpPost("{id}/toggle-complete")]
        public async Task<IActionResult> ToggleComplete(int id)
        {
            var f = await _context.FollowUps.FindAsync(id);
            if (f == null)
            {
                return NotFound(new { message = $"Task with ID {id} not found." });
            }

            if (f.Status == FollowUpStatus.Completed)
            {
                f.Status = FollowUpStatus.Pending;
                f.CompletedAt = null;
            }
            else
            {
                f.Status = FollowUpStatus.Completed;
                f.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { status = f.Status.ToString(), completedAt = f.CompletedAt });
        }

        // DELETE: api/FollowUpsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFollowUp(int id)
        {
            var f = await _context.FollowUps.FindAsync(id);
            if (f == null)
            {
                return NotFound(new { message = $"Task with ID {id} not found." });
            }

            _context.FollowUps.Remove(f);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("FollowUp", id.ToString(), "ApiDelete",
                $"API deleted task '{f.Subject}'.");

            return NoContent();
        }
    }
}
