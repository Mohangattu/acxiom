using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCrm.Models
{
    public class Lead
    {
        public int Id { get; set; }

        [NotMapped]
        [Display(Name = "Lead ID")]
        public int LeadId => Id;

        [StringLength(20)]
        [Display(Name = "Lead Code")]
        public string? LeadCode { get; set; }

        [Required(ErrorMessage = "Lead name is required.")]
        [StringLength(100, ErrorMessage = "Lead name cannot exceed 100 characters.")]
        [Display(Name = "Lead Name")]
        public string? LeadName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [NotMapped]
        [Display(Name = "Full Name")]
        public string FullName => !string.IsNullOrWhiteSpace(LeadName) ? LeadName : $"{FirstName} {LastName}".Trim();

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(@"^[0-9+\-\(\) ]{7,20}$", ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string? Phone { get; set; }

        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
        public string? Company { get; set; }

        [NotMapped]
        [Display(Name = "Company Name")]
        public string? CompanyName
        {
            get => Company;
            set => Company = value;
        }

        [StringLength(50, ErrorMessage = "Lead source cannot exceed 50 characters.")]
        [Display(Name = "Lead Source")]
        public string? Source { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [Display(Name = "Status")]
        public LeadStatus Status { get; set; } = LeadStatus.New;

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Range(0, 100000000, ErrorMessage = "Expected value must be between 0 and 100,000,000.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Expected Value ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedValue { get; set; }

        [NotMapped]
        [Display(Name = "Expected Value ($)")]
        public decimal? ExpectedValue
        {
            get => EstimatedValue;
            set => EstimatedValue = value;
        }

        [Display(Name = "Assigned Sales Rep")]
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        [NotMapped]
        [Display(Name = "Assigned To")]
        public string? AssignedTo => AssignedToUser?.FullName ?? AssignedToUserId;

        [Display(Name = "Converted Customer")]
        public int? ConvertedCustomerId { get; set; }
        public Customer? ConvertedCustomer { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }

        [Display(Name = "Converted Date")]
        public DateTime? ConvertedAt { get; set; }

        // Navigation
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    }
}
