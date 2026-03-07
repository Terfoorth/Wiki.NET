using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities;

public class ReminderEmailDispatch
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public UserNotificationType Type { get; set; }

    public int SourceEntityId { get; set; }

    public int StageDaysBefore { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public int AttemptCount { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
