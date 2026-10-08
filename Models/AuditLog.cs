using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Entity / Module")]
        public string EntityName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Record ID")]
        public string? EntityId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Action")]
        public string Action { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "Details")]
        public string Details { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "User ID")]
        public string? UserId { get; set; }

        [StringLength(100)]
        [Display(Name = "Performed By")]
        public string? UserName { get; set; }

        [Display(Name = "Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        [Display(Name = "IP Address")]
        public string? IpAddress { get; set; }
    }
}
