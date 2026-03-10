using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities;

public class AppNotification
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public NotificationKind Kind { get; set; }

    public int SourceId { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime TriggerDate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    [Required]
    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

public enum NotificationKind
{
    WikiReviewDate = 1,
    OnboardingStartDate = 2,
    OnboardingTargetDate = 3,
    HomeCommentOwner = 4,
    HomeCommentMention = 5
}
