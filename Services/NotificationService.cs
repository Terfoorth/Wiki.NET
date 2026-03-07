using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class NotificationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IReminderCandidateProvider reminderCandidateProvider) : INotificationService
{
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

        var timeZone = ReminderCandidateProvider.ResolveTimeZone(timeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        var candidates = await reminderCandidateProvider.GetCandidatesAsync(
            context,
            normalizedUserId,
            todayLocal,
            timeZone,
            cancellationToken);

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
        IReadOnlyCollection<ReminderCandidate> candidates)
    {
        var nowUtc = DateTime.UtcNow;

        var candidateByKey = candidates
            .GroupBy(CreateKey)
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

    private static NotificationKey CreateKey(UserNotification notification)
    {
        return new NotificationKey(
            notification.Type,
            notification.SourceEntityId,
            notification.StageDaysBefore,
            DateOnly.FromDateTime(notification.DueDate));
    }

    private static NotificationKey CreateKey(ReminderCandidate candidate)
    {
        return new NotificationKey(
            candidate.Type,
            candidate.SourceEntityId,
            candidate.StageDaysBefore,
            candidate.DueDate);
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

    private readonly record struct NotificationKey(
        UserNotificationType Type,
        int SourceEntityId,
        int StageDaysBefore,
        DateOnly DueDate);
}
