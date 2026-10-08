using AcxiomCrm.Models;

namespace AcxiomCrm.ViewModels
{
    public class DashboardViewModel
    {
        // KPI Summary Counters
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int ActiveOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WonDealsValue { get; set; }
        public int PendingFollowUpsCount { get; set; }
        public int OverdueFollowUpsCount { get; set; }

        // Chart Data: Leads by Status
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusCounts { get; set; } = new();

        // Chart Data: Opportunities by Stage
        public List<string> OpportunityStageLabels { get; set; } = new();
        public List<int> OpportunityStageCounts { get; set; } = new();
        public List<decimal> OpportunityStageValues { get; set; } = new();

        // Lists for widget tables
        public List<FollowUp> UpcomingFollowUps { get; set; } = new();
        public List<Opportunity> RecentOpportunities { get; set; } = new();
        public List<AuditLog> RecentAuditLogs { get; set; } = new();
    }
}
