using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.Models.Dtos;

namespace AcxiomCrm.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/DashboardApi/stats
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            var oppData = await _context.Opportunities
                .Select(o => new { o.Stage, o.Amount })
                .ToListAsync();

            var stats = new DashboardStatsDto
            {
                TotalCustomers = await _context.Customers.CountAsync(),
                TotalLeads = await _context.Leads.CountAsync(),
                ActiveOpportunities = oppData.Count(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost),
                TotalPipelineValue = oppData.Where(o => o.Stage != OpportunityStage.ClosedLost).Sum(o => o.Amount),
                WonDealsValue = oppData.Where(o => o.Stage == OpportunityStage.ClosedWon).Sum(o => o.Amount),
                PendingFollowUpsCount = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending)
            };

            return Ok(stats);
        }
    }
}
