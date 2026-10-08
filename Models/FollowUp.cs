using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Models
{
    public class FollowUp
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150)]
        [Display(Name = "Subject / Task")]
        public string Subject { get; set; } = string.Empty;

        [Display(Name = "Activity Type")]
        public FollowUpType Type { get; set; } = FollowUpType.Call;

        [Required(ErrorMessage = "Due date and time is required.")]
        [Display(Name = "Due Date & Time")]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(1);

        [Display(Name = "Status")]
        public FollowUpStatus Status { get; set; } = FollowUpStatus.Pending;

        [Display(Name = "Priority")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Associated records (can link to Lead, Customer, or Opportunity)
        [Display(Name = "Related Customer")]
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Display(Name = "Related Lead")]
        public int? LeadId { get; set; }
        public Lead? Lead { get; set; }

        [Display(Name = "Related Opportunity")]
        public int? OpportunityId { get; set; }
        public Opportunity? Opportunity { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Display(Name = "Assigned To")]
        public string? AssignedTo => AssignedToUser?.FullName;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate => CreatedAt;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Display(Name = "Date")]
        public DateTime Date => DueDate;

        [Display(Name = "Completed Date")]
        public DateTime? CompletedAt { get; set; }
    }
}
