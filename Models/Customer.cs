using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCrm.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [StringLength(20)]
        [Display(Name = "Customer Code")]
        public string? CustomerCode { get; set; }

        [Required(ErrorMessage = "Customer name is required.")]
        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
        [Display(Name = "Customer Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [RegularExpression(@"^[0-9+\-\(\) ]{7,20}$", ErrorMessage = "Invalid phone number format.")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
        [Display(Name = "Company Name")]
        public string? Company { get; set; }

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
        public string? Address { get; set; }

        [StringLength(50, ErrorMessage = "City cannot exceed 50 characters.")]
        public string? City { get; set; }

        [StringLength(50, ErrorMessage = "State cannot exceed 50 characters.")]
        public string? State { get; set; }

        [StringLength(50)]
        public string? Industry { get; set; }

        [Display(Name = "Status")]
        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        [Display(Name = "Assigned Sales Rep")]
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime? UpdatedAt { get; set; }

        // Convenient domain aliases matching assignment requirements
        [NotMapped]
        public int CustomerId => Id;

        [NotMapped]
        public string CustomerName
        {
            get => Name;
            set => Name = value;
        }

        [NotMapped]
        public string? CompanyName
        {
            get => Company;
            set => Company = value;
        }

        [NotMapped]
        public DateTime CreatedDate => CreatedAt;

        // Navigation properties
        public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    }
}
