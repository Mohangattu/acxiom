using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.ViewModels;

namespace AcxiomCrm.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;

            // Load opportunities stage & amount into memory to avoid SQLite decimal sum limitation
            var oppData = await _context.Opportunities
                .Select(o => new { o.Stage, o.Amount })
                .ToListAsync();

            var totalPipeline = oppData.Where(o => o.Stage != OpportunityStage.ClosedLost).Sum(o => o.Amount);
            var wonRevenue = oppData.Where(o => o.Stage == OpportunityStage.ClosedWon).Sum(o => o.Amount);

            var model = new DashboardViewModel
            {
                TotalCustomers = await _context.Customers.CountAsync(),
                TotalLeads = await _context.Leads.CountAsync(),
                OpenLeads = await _context.Leads.CountAsync(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost),
                ConvertedLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.Converted),
                ActiveOpportunities = oppData.Count(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost),
                TotalPipelineValue = totalPipeline,
                WonDealsValue = wonRevenue,
                PendingFollowUpsCount = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending),
                OverdueFollowUpsCount = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.DueDate < now)
            };

            // 1. Leads by Status Breakdown
            var leadStatuses = Enum.GetValues(typeof(LeadStatus)).Cast<LeadStatus>();
            foreach (var status in leadStatuses)
            {
                var count = await _context.Leads.CountAsync(l => l.Status == status);
                model.LeadStatusLabels.Add(status.ToString());
                model.LeadStatusCounts.Add(count);
            }

            // 2. Opportunities by Stage Breakdown
            var oppStages = Enum.GetValues(typeof(OpportunityStage)).Cast<OpportunityStage>();
            foreach (var stage in oppStages)
            {
                var count = oppData.Count(o => o.Stage == stage);
                var value = oppData.Where(o => o.Stage == stage).Sum(o => o.Amount);
                
                model.OpportunityStageLabels.Add(stage.ToString());
                model.OpportunityStageCounts.Add(count);
                model.OpportunityStageValues.Add(value);
            }

            // 3. Upcoming Follow-Ups (Pending, closest due date)
            model.UpcomingFollowUps = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .Where(f => f.Status == FollowUpStatus.Pending)
                .OrderBy(f => f.DueDate)
                .Take(5)
                .ToListAsync();

            // 4. Recent Opportunities
            model.RecentOpportunities = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 5. Recent System Activities
            model.RecentAuditLogs = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(6)
                .ToListAsync();

            return View(model);
        }
    }
}
