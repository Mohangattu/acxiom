using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Models;

namespace AcxiomCrm.Models.Dtos
{
    public class CustomerDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Industry { get; set; }
        public CustomerStatus Status { get; set; } = CustomerStatus.Active;
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LeadDto
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? Source { get; set; }
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public string? Notes { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public int? ConvertedCustomerId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OpportunityDto
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        [Required]
        [Range(0.01, 100000000)]
        public decimal Amount { get; set; }

        public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;
        public int Probability { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public string? Notes { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FollowUpDto
    {
        public int Id { get; set; }

        [Required]
        public string Subject { get; set; } = string.Empty;

        public FollowUpType Type { get; set; } = FollowUpType.Call;
        public DateTime DueDate { get; set; }
        public FollowUpStatus Status { get; set; } = FollowUpStatus.Pending;
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
        public string? Notes { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public int? OpportunityId { get; set; }
        public string? OpportunityTitle { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class DashboardStatsDto
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int ActiveOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WonDealsValue { get; set; }
        public int PendingFollowUpsCount { get; set; }
    }
}
