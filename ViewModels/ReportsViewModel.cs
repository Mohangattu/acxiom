using AcxiomCrm.Models;

namespace AcxiomCrm.ViewModels
{
    public class ReportsViewModel
    {
        // Customer Metrics
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int InactiveCustomers { get; set; }
        public int PendingCustomers { get; set; }
        public List<KeyValuePair<string, int>> CustomersByIndustry { get; set; } = new();

        // Lead Metrics
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }
        public int ContactedLeads { get; set; }
        public int QualifiedLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int LostLeads { get; set; }
        public decimal LeadConversionRate => TotalLeads > 0 ? ((decimal)ConvertedLeads / TotalLeads * 100) : 0m;

        // Opportunity Metrics
        public int TotalOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public decimal WonRevenue { get; set; }
        public int WonCount { get; set; }
        public int LostCount { get; set; }
        public decimal WinRate => (WonCount + LostCount) > 0 ? ((decimal)WonCount / (WonCount + LostCount) * 100) : 0m;
        public List<StageSummaryItem> PipelineByStage { get; set; } = new();

        // Follow-Up Metrics
        public int TotalFollowUps { get; set; }
        public int PendingFollowUps { get; set; }
        public int CompletedFollowUps { get; set; }
        public int OverdueFollowUps { get; set; }
        public List<KeyValuePair<string, int>> FollowUpsByType { get; set; } = new();

        // Sales Rep Leaderboard
        public List<SalesRepPerformanceItem> SalesRepPerformance { get; set; } = new();
    }

    public class StageSummaryItem
    {
        public string StageName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class SalesRepPerformanceItem
    {
        public string RepName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ActiveDeals { get; set; }
        public decimal WonRevenue { get; set; }
        public int TasksCompleted { get; set; }
    }
}
