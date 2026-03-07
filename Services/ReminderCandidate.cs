using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public sealed record ReminderCandidate(
    UserNotificationType Type,
    int SourceEntityId,
    int StageDaysBefore,
    DateOnly DueDate,
    string Title,
    string Message,
    string TargetUrl);
