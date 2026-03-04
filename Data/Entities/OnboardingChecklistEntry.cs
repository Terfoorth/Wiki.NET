using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities;

public class OnboardingChecklistEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProfileId { get; set; }

    [Required]
    public int CatalogItemId { get; set; }

    public bool IsCompleted { get; set; }

    public OnboardingChecklistResult Result { get; set; } = OnboardingChecklistResult.Pending;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public OnboardingProfile? Profile { get; set; }

    [ForeignKey(nameof(CatalogItemId))]
    public OnboardingChecklistCatalogItem? CatalogItem { get; set; }
}

public enum OnboardingChecklistResult
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
    NotApplicable = 3
}
