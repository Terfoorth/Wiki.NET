using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities;

public class AdminAuditLog
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string AdminUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AdminUserId))]
    public ApplicationUser? AdminUser { get; set; }
}
