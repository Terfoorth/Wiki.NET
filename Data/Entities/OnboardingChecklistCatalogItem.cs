using System.ComponentModel.DataAnnotations;

namespace Wiki_Blaze.Data.Entities;

public class OnboardingChecklistCatalogItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OnboardingChecklistEntry> Entries { get; set; } = new List<OnboardingChecklistEntry>();
}
