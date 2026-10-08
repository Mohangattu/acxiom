using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCrm.Data;
using AcxiomCrm.Models;
using AcxiomCrm.ViewModels;

namespace AcxiomCrm.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Reports
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;

            var model = new ReportsViewModel
            {
                // Customer stats
                TotalCustomers = await _context.Customers.CountAsync(),
                ActiveCustomers = await _context.Customers.CountAsync(c => c.Status == CustomerStatus.Active),
                InactiveCustomers = await _context.Customers.CountAsync(c => c.Status == CustomerStatus.Inactive),
                PendingCustomers = await _context.Customers.CountAsync(c => c.Status == CustomerStatus.Pending),

                // Lead stats
                TotalLeads = await _context.Leads.CountAsync(),
                NewLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.New),
                ContactedLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.Contacted),
                QualifiedLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.Qualified),
                ConvertedLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.Converted),
                LostLeads = await _context.Leads.CountAsync(l => l.Status == LeadStatus.Lost),

                // FollowUp stats
                TotalFollowUps = await _context.FollowUps.CountAsync(),
                PendingFollowUps = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending),
                CompletedFollowUps = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Completed),
                OverdueFollowUps = await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.DueDate < now)
            };

            // Customers by Industry
            var industries = await _context.Customers
                .Where(c => !string.IsNullOrEmpty(c.Industry))
                .GroupBy(c => c.Industry!)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .ToListAsync();
            model.CustomersByIndustry = industries;

            // Opportunities calculation in-memory
            var opps = await _context.Opportunities.ToListAsync();
            model.TotalOpportunities = opps.Count;

            var activeOpps = opps.Where(o => o.Stage != OpportunityStage.ClosedLost && o.Stage != OpportunityStage.ClosedWon).ToList();
            model.TotalPipelineValue = activeOpps.Sum(o => o.Amount);
            model.WeightedPipelineValue = activeOpps.Sum(o => o.Amount * o.Probability / 100m);

            var wonOpps = opps.Where(o => o.Stage == OpportunityStage.ClosedWon).ToList();
            model.WonRevenue = wonOpps.Sum(o => o.Amount);
            model.WonCount = wonOpps.Count;
            model.LostCount = opps.Count(o => o.Stage == OpportunityStage.ClosedLost);

            // Pipeline by Stage
            foreach (OpportunityStage stage in Enum.GetValues(typeof(OpportunityStage)))
            {
                var stageOpps = opps.Where(o => o.Stage == stage).ToList();
                model.PipelineByStage.Add(new StageSummaryItem
                {
                    StageName = stage == OpportunityStage.ClosedWon ? "Won" : stage == OpportunityStage.ClosedLost ? "Lost" : stage.ToString(),
                    Count = stageOpps.Count,
                    TotalAmount = stageOpps.Sum(o => o.Amount),
                    WeightedAmount = stageOpps.Sum(o => o.Amount * o.Probability / 100m)
                });
            }

            // Follow-Ups by Type
            foreach (FollowUpType fType in Enum.GetValues(typeof(FollowUpType)))
            {
                var count = await _context.FollowUps.CountAsync(f => f.Type == fType);
                model.FollowUpsByType.Add(new KeyValuePair<string, int>(fType.ToString(), count));
            }

            // Sales Rep Performance
            var activeUsers = await _userManager.Users.Where(u => u.IsActive).ToListAsync();
            foreach (var user in activeUsers)
            {
                var userOpps = opps.Where(o => o.AssignedToUserId == user.Id).ToList();
                var activeCount = userOpps.Count(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost);
                var wonAmt = userOpps.Where(o => o.Stage == OpportunityStage.ClosedWon).Sum(o => o.Amount);
                var tasksDone = await _context.FollowUps.CountAsync(f => f.AssignedToUserId == user.Id && f.Status == FollowUpStatus.Completed);

                model.SalesRepPerformance.Add(new SalesRepPerformanceItem
                {
                    RepName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    ActiveDeals = activeCount,
                    WonRevenue = wonAmt,
                    TasksCompleted = tasksDone
                });
            }

            return View(model);
        }

        // GET: Reports/ExportSummaryCsv
        public async Task<IActionResult> ExportSummaryCsv()
        {
            var opps = await _context.Opportunities.ToListAsync();
            var totalPipeline = opps.Where(o => o.Stage != OpportunityStage.ClosedLost && o.Stage != OpportunityStage.ClosedWon).Sum(o => o.Amount);
            var wonRevenue = opps.Where(o => o.Stage == OpportunityStage.ClosedWon).Sum(o => o.Amount);

            var csv = new StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Customers,{await _context.Customers.CountAsync()}");
            csv.AppendLine($"Active Customers,{await _context.Customers.CountAsync(c => c.Status == CustomerStatus.Active)}");
            csv.AppendLine($"Total Leads,{await _context.Leads.CountAsync()}");
            csv.AppendLine($"Converted Leads,{await _context.Leads.CountAsync(l => l.Status == LeadStatus.Converted)}");
            csv.AppendLine($"Total Opportunities,{opps.Count}");
            csv.AppendLine($"Active Pipeline Value,{totalPipeline}");
            csv.AppendLine($"Closed Won Revenue,{wonRevenue}");
            csv.AppendLine($"Total Follow-Ups,{await _context.FollowUps.CountAsync()}");
            csv.AppendLine($"Completed Follow-Ups,{await _context.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Completed)}");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"CRM_Summary_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}
