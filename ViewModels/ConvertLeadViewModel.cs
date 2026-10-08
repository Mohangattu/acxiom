using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Models;

namespace AcxiomCrm.ViewModels
{
    public class ConvertLeadViewModel
    {
        public int LeadId { get; set; }
        public string LeadFullName { get; set; } = string.Empty;

        // Customer Details to create
        [Required(ErrorMessage = "Customer name is required.")]
        [Display(Name = "Account / Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? Industry { get; set; }

        [Display(Name = "Assigned Sales Rep")]
        public string? AssignedToUserId { get; set; }

        // Opportunity Option
        [Display(Name = "Create an initial Opportunity for this customer")]
        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Title")]
        public string? OpportunityTitle { get; set; }

        [Range(0.01, 100000000, ErrorMessage = "Opportunity amount must be greater than 0.")]
        [Display(Name = "Opportunity Amount ($)")]
        public decimal? OpportunityAmount { get; set; }

        [Display(Name = "Opportunity Stage")]
        public OpportunityStage OpportunityStage { get; set; } = OpportunityStage.Prospecting;

        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddMonths(1);
    }
}
