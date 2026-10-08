using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCrm.Models
{
    public class Opportunity
    {
        public int Id { get; set; }

        [NotMapped]
        [Display(Name = "Opportunity ID")]
        public int OpportunityId => Id;

        [StringLength(20)]
        [Display(Name = "Opportunity Code")]
        public string? OpportunityCode { get; set; }

        [Required(ErrorMessage = "Opportunity name is required.")]
        [StringLength(150, ErrorMessage = "Opportunity name cannot exceed 150 characters.")]
        [Display(Name = "Opportunity Name")]
        public string Title { get; set; } = string.Empty;

        [NotMapped]
        [Display(Name = "Name")]
        public string Name => Title;

        [NotMapped]
        [Display(Name = "Opportunity Name")]
        public string OpportunityName => Title;

        [Required(ErrorMessage = "Please select a customer.")]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required(ErrorMessage = "Deal value/amount is required.")]
        [Range(0.01, 100000000.00, ErrorMessage = "Amount must be greater than 0.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Deal Value ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [NotMapped]
        [Display(Name = "Expected Value")]
        public decimal ExpectedValue => Amount;

        [Display(Name = "Stage")]
        public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100%.")]
        [Display(Name = "Probability (%)")]
        public int Probability { get; set; } = 30;

        [NotMapped]
        [Display(Name = "Weighted Pipeline Value ($)")]
        public decimal WeightedValue => Math.Round(Amount * Probability / 100m, 2);

        [Required(ErrorMessage = "Expected close date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddMonths(1);

        [StringLength(100)]
        [Display(Name = "Lead / Deal Source")]
        public string? Source { get; set; }

        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Assigned Sales Rep")]
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        [NotMapped]
        [Display(Name = "Assigned To")]
        public string? AssignedTo => AssignedToUser?.FullName;

        [NotMapped]
        [Display(Name = "Owner")]
        public string? Owner => AssignedToUser?.FullName;

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate => CreatedAt;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate => UpdatedAt;

        [StringLength(500)]
        [Display(Name = "Win / Loss Reason")]
        public string? WinLossReason { get; set; }

        // Navigation
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    }
}
