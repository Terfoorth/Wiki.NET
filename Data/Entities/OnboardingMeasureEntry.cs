using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities;

public class OnboardingMeasureEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProfileId { get; set; }

    [Required]
    public int CatalogItemId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Value { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public OnboardingProfile? Profile { get; set; }

    [ForeignKey(nameof(CatalogItemId))]
    public OnboardingMeasureCatalogItem? CatalogItem { get; set; }
}
