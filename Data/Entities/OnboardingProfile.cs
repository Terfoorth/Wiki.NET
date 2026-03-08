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

    public OnboardingSalutation Salutation { get; set; } = OnboardingSalutation.Unspecified;

    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(120)]
    public string? TicketNumber { get; set; }

    public DateTime EntryDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime? ExitDate { get; set; }

    [MaxLength(150)]
    public string? Department { get; set; }

    [MaxLength(150)]
    public string? Supervisor { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Mobile { get; set; }

    [MaxLength(150)]
    public string? Location { get; set; }

    [MaxLength(150)]
    public string? JobTitle { get; set; }

    [MaxLength(120)]
    public string? Hostname { get; set; }

    [MaxLength(120)]
    public string? DeviceNumber { get; set; }

    [MaxLength(120)]
    public string? PrinterCardNumber { get; set; }

    [MaxLength(450)]
    public string? LinkedUserId { get; set; }

    [MaxLength(450)]
    public string? AssignedAgentUserId { get; set; }

    public OnboardingProfileStatus Status { get; set; } = OnboardingProfileStatus.NotStarted;

    public DateTime? StartDate { get; set; }

    public DateTime? TargetDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(LinkedUserId))]
    public ApplicationUser? LinkedUser { get; set; }

    [ForeignKey(nameof(AssignedAgentUserId))]
    public ApplicationUser? AssignedAgentUser { get; set; }

    public OnboardingProfileAttachment? Attachment { get; set; }

    public ICollection<OnboardingMeasureEntry> MeasureEntries { get; set; } = new List<OnboardingMeasureEntry>();

    public ICollection<OnboardingChecklistEntry> ChecklistEntries { get; set; } = new List<OnboardingChecklistEntry>();
}

public enum OnboardingSalutation
{
    Unspecified = 0,
    Mr = 1,
    Mrs = 2,
    Diverse = 3
}

public enum OnboardingProfileStatus
{
    Draft = 0,
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Archived = 4
}
