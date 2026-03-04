using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities;

public class OnboardingProfile
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Department { get; set; }

    [MaxLength(150)]
    public string? Supervisor { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(120)]
    public string? PrinterCardNumber { get; set; }

    [MaxLength(450)]
    public string? LinkedUserId { get; set; }

    public OnboardingProfileStatus Status { get; set; } = OnboardingProfileStatus.InProgress;

    public DateTime? StartDate { get; set; }

    public DateTime? TargetDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(LinkedUserId))]
    public ApplicationUser? LinkedUser { get; set; }

    public ICollection<OnboardingMeasureEntry> MeasureEntries { get; set; } = new List<OnboardingMeasureEntry>();

    public ICollection<OnboardingChecklistEntry> ChecklistEntries { get; set; } = new List<OnboardingChecklistEntry>();
}

public enum OnboardingProfileStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Archived = 3
}
