using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class NotificationService(IDbContextFactory<ApplicationDbContext> dbFactory) : INotificationService
{
    private static readonly HashSet<int> ReminderStagesInDays = [3, 1];

    private static readonly string[] LegacyDateFormats =
    [
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy"
    ];

    public async Task<IReadOnlyList<UserNotification>> RefreshAndGetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (normalizedUserId is null)
        {
            return Array.Empty<UserNotification>();
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var timeZoneId = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == normalizedUserId)
            .Select(user => user.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ResolveTimeZone(timeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        var candidates = new List<NotificationCandidate>();
        candidates.AddRange(await BuildWikiReviewCandidatesAsync(context, normalizedUserId, todayLocal, cancellationToken));
        candidates.AddRange(await BuildOnboardingCandidatesAsync(context, normalizedUserId, todayLocal, timeZone, cancellationToken));

        var existing = await context.UserNotifications
            .Where(notification => notification.UserId == normalizedUserId)
            .ToListAsync(cancellationToken);

        MergeNotifications(context, existing, normalizedUserId, candidates);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race-condition fallback: another request inserted the same notification key.
        }

        return await LoadActiveNotificationsAsync(normalizedUserId, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(string userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (normalizedUserId is null)
        {
            return false;
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var notification = await context.UserNotifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == normalizedUserId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (normalizedUserId is null)
        {
            return 0;
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var unreadNotifications = await context.UserNotifications
            .Where(notification =>
                notification.UserId == normalizedUserId
                && !notification.IsArchived
                && !notification.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return 0;
        }

        var readAtUtc = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = readAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
        return unreadNotifications.Count;
    }

    private async Task<IReadOnlyList<UserNotification>> LoadActiveNotificationsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await context.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && !notification.IsArchived)
            .OrderBy(notification => notification.IsRead)
            .ThenBy(notification => notification.DueDate)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    private static void MergeNotifications(
        ApplicationDbContext context,
        IReadOnlyCollection<UserNotification> existingNotifications,
        string userId,
        IReadOnlyCollection<NotificationCandidate> candidates)
    {
        var nowUtc = DateTime.UtcNow;

        var candidateByKey = candidates
            .GroupBy(candidate => candidate.Key)
            .ToDictionary(group => group.Key, group => group.First());

        var existingByKey = existingNotifications
            .GroupBy(CreateKey)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id).ToList());

        foreach (var pair in candidateByKey)
        {
            var candidate = pair.Value;
            if (existingByKey.TryGetValue(pair.Key, out var sameKeyNotifications))
            {
                var primary = sameKeyNotifications[0];
                primary.DueDate = ToUtcDate(candidate.DueDate);
                primary.Title = candidate.Title;
                primary.Message = candidate.Message;
                primary.TargetUrl = candidate.TargetUrl;
                primary.IsArchived = false;
                primary.ArchivedAtUtc = null;

                for (var i = 1; i < sameKeyNotifications.Count; i++)
                {
                    var duplicate = sameKeyNotifications[i];
                    duplicate.IsArchived = true;
                    duplicate.ArchivedAtUtc ??= nowUtc;
                }

                continue;
            }

            context.UserNotifications.Add(new UserNotification
            {
                UserId = userId,
                Type = candidate.Type,
                SourceEntityId = candidate.SourceEntityId,
                StageDaysBefore = candidate.StageDaysBefore,
                DueDate = ToUtcDate(candidate.DueDate),
                Title = candidate.Title,
                Message = candidate.Message,
                TargetUrl = candidate.TargetUrl,
                IsRead = false,
                ReadAtUtc = null,
                IsArchived = false,
                ArchivedAtUtc = null,
                CreatedAtUtc = nowUtc
            });
        }

        var activeKeys = candidateByKey.Keys.ToHashSet();
        foreach (var existing in existingNotifications)
        {
            var key = CreateKey(existing);
            if (activeKeys.Contains(key))
            {
                continue;
            }

            if (!existing.IsArchived)
            {
                existing.IsArchived = true;
                existing.ArchivedAtUtc = nowUtc;
            }
        }
    }

    private static async Task<List<NotificationCandidate>> BuildWikiReviewCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        CancellationToken cancellationToken)
    {
        var rawValues = await context.WikiPageAttributeValues
            .AsNoTracking()
            .Where(value =>
                value.WikiPage != null
                && value.WikiPage.OwnerId == userId
                && value.AttributeDefinition != null
                && value.AttributeDefinition.Name == "ReviewDate")
            .Select(value => new
            {
                value.WikiPageId,
                PageTitle = value.WikiPage!.Title,
                value.Value
            })
            .ToListAsync(cancellationToken);

        var result = new List<NotificationCandidate>();
        foreach (var item in rawValues)
        {
            if (!TryParseLegacyDate(item.Value, out var dueDate))
            {
                continue;
            }

            var daysUntilDue = dueDate.DayNumber - todayLocal.DayNumber;
            if (!ReminderStagesInDays.Contains(daysUntilDue))
            {
                continue;
            }

            var dayLabel = daysUntilDue == 1 ? "Tag" : "Tagen";
            var dueDateDisplay = dueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            result.Add(new NotificationCandidate(
                UserNotificationType.WikiReviewDate,
                item.WikiPageId,
                daysUntilDue,
                dueDate,
                $"Review in {daysUntilDue} {dayLabel}",
                $"Der Wiki-Eintrag \"{item.PageTitle}\" erreicht sein Review-Datum am {dueDateDisplay}.",
                $"/wiki/view/{item.WikiPageId}"));
        }

        return result;
    }

    private static async Task<List<NotificationCandidate>> BuildOnboardingCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone,
        CancellationToken cancellationToken)
    {
        var profiles = await context.OnboardingProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.AssignedAgentUserId == userId
                && profile.Status != OnboardingProfileStatus.Completed
                && profile.Status != OnboardingProfileStatus.Archived)
            .Select(profile => new
            {
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.FullName,
                profile.StartDate,
                profile.TargetDate
            })
            .ToListAsync(cancellationToken);

        var result = new List<NotificationCandidate>();

        foreach (var profile in profiles)
        {
            var profileName = BuildProfileName(profile.Id, profile.FirstName, profile.LastName, profile.FullName);
            if (profile.StartDate.HasValue)
            {
                AddOnboardingCandidate(
                    result,
                    profile.Id,
                    profileName,
                    profile.StartDate.Value,
                    UserNotificationType.OnboardingStartDate,
                    "Startdatum",
                    todayLocal,
                    userTimeZone);
            }

            if (profile.TargetDate.HasValue)
            {
                AddOnboardingCandidate(
                    result,
                    profile.Id,
                    profileName,
                    profile.TargetDate.Value,
                    UserNotificationType.OnboardingTargetDate,
                    "Zieldatum",
                    todayLocal,
                    userTimeZone);
            }
        }

        return result;
    }

    private static void AddOnboardingCandidate(
        ICollection<NotificationCandidate> target,
        int profileId,
        string profileName,
        DateTime dueDateValue,
        UserNotificationType type,
        string label,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone)
    {
        var dueDateLocal = ToDateOnlyInTimeZone(dueDateValue, userTimeZone);
        var daysUntilDue = dueDateLocal.DayNumber - todayLocal.DayNumber;

        if (!ReminderStagesInDays.Contains(daysUntilDue))
        {
            return;
        }

        var dayLabel = daysUntilDue == 1 ? "Tag" : "Tagen";
        var dueDateDisplay = dueDateLocal.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        target.Add(new NotificationCandidate(
            type,
            profileId,
            daysUntilDue,
            dueDateLocal,
            $"{label} in {daysUntilDue} {dayLabel}",
            $"Beim Onboarding-Profil \"{profileName}\" liegt das {label} am {dueDateDisplay}.",
            $"/reportdesigner/details/{profileId}"));
    }

    private static DateOnly ToDateOnlyInTimeZone(DateTime value, TimeZoneInfo timeZone)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(value, timeZone));
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, timeZone));
        }

        return DateOnly.FromDateTime(value);
    }

    private static bool TryParseLegacyDate(string? value, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, LegacyDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var invariantDateTime))
        {
            date = DateOnly.FromDateTime(invariantDateTime);
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var currentDateTime))
        {
            date = DateOnly.FromDateTime(currentDateTime);
            return true;
        }

        return false;
    }

    private static NotificationKey CreateKey(UserNotification notification)
    {
        return new NotificationKey(
            notification.Type,
            notification.SourceEntityId,
            notification.StageDaysBefore,
            DateOnly.FromDateTime(notification.DueDate));
    }

    private static DateTime ToUtcDate(DateOnly date)
    {
        return DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return false;
    }

    private static string? NormalizeUserId(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static string BuildProfileName(int profileId, string? firstName, string? lastName, string? fullName)
    {
        var combined = string.Join(" ", new[] { firstName?.Trim(), lastName?.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        return string.IsNullOrWhiteSpace(fullName) ? $"Profil #{profileId}" : fullName;
    }

    private readonly record struct NotificationKey(
        UserNotificationType Type,
        int SourceEntityId,
        int StageDaysBefore,
        DateOnly DueDate);

    private sealed record NotificationCandidate(
        UserNotificationType Type,
        int SourceEntityId,
        int StageDaysBefore,
        DateOnly DueDate,
        string Title,
        string Message,
        string TargetUrl)
    {
        public NotificationKey Key => new(Type, SourceEntityId, StageDaysBefore, DueDate);
    }
}
