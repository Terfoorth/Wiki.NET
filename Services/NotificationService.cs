using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;
using Wiki_Blaze.Services.Notifications;

namespace Wiki_Blaze.Services;

public class NotificationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<NotificationService> logger) : INotificationService, INotificationSchedulerService
{
    private static readonly NotificationKind[] ManagedNotificationKinds =
    [
        NotificationKind.WikiReviewDate,
        NotificationKind.OnboardingStartDate,
        NotificationKind.OnboardingTargetDate,
        NotificationKind.OnboardingTodoUpcoming
    ];

    private static readonly TimeSpan ReminderTriggerLocalTime = new(8, 0, 0);

    public async Task<NotificationInboxDto> GetInboxAsync(
        string userId,
        int take = 20,
        bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new NotificationInboxDto();
        }

        var boundedTake = Math.Clamp(take, 1, 200);

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var unreadCount = await context.AppNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .CountAsync(cancellationToken);

        var query = context.AppNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (!includeRead)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        var items = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(boundedTake)
            .Select(notification => new NotificationInboxItemDto
            {
                Id = notification.Id,
                Kind = notification.Kind,
                SourceId = notification.SourceId,
                Title = notification.Title,
                Body = notification.Body,
                TargetUrl = notification.TargetUrl,
                DueDate = notification.DueDate,
                TriggerDate = notification.TriggerDate,
                CreatedAtUtc = notification.CreatedAtUtc,
                IsRead = notification.IsRead
            })
            .ToListAsync(cancellationToken);

        return new NotificationInboxDto
        {
            Items = items,
            UnreadCount = unreadCount
        };
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await context.AppNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(string userId, int notificationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || notificationId <= 0)
        {
            return false;
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);
        var notification = await context.AppNotifications
            .FirstOrDefaultAsync(
                entry => entry.Id == notificationId && entry.UserId == userId,
                cancellationToken);

        if (notification is null || notification.IsRead)
        {
            return notification is not null;
        }

        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<WikiReviewAlertStateDto>> GetWikiReviewAlertStatesAsync(
        string userId,
        IEnumerable<int> wikiPageIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<WikiReviewAlertStateDto>();
        }

        var pageIds = wikiPageIds
            .Where(pageId => pageId > 0)
            .Distinct()
            .ToList();

        if (pageIds.Count == 0)
        {
            return Array.Empty<WikiReviewAlertStateDto>();
        }

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var timeZoneInfo = await ResolveUserTimeZoneAsync(context, userId, cancellationToken);
        var localDate = ConvertUtcToLocal(DateTime.UtcNow, timeZoneInfo).Date;

        var reviewAttributeDefinitionId = await context.WikiAttributeDefinitions
            .AsNoTracking()
            .Where(definition => definition.Name == "ReviewDate")
            .Select(definition => definition.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (reviewAttributeDefinitionId == 0)
        {
            return Array.Empty<WikiReviewAlertStateDto>();
        }

        var reviewRows = await (
            from value in context.WikiPageAttributeValues.AsNoTracking()
            join page in context.WikiPages.AsNoTracking() on value.WikiPageId equals page.Id
            where value.AttributeDefinitionId == reviewAttributeDefinitionId
                && page.OwnerId == userId
                && pageIds.Contains(page.Id)
            select new
            {
                PageId = page.Id,
                RawValue = value.Value
            })
            .ToListAsync(cancellationToken);

        var result = new List<WikiReviewAlertStateDto>();
        foreach (var row in reviewRows)
        {
            if (!NotificationDateParser.TryParseToDate(row.RawValue, out var dueDate))
            {
                continue;
            }

            var firstReminderDate = NotificationScheduleCalculator.GetFirstReminderDate(dueDate);
            if (localDate < firstReminderDate)
            {
                continue;
            }

            result.Add(new WikiReviewAlertStateDto
            {
                PageId = row.PageId,
                IsAlert = true,
                IsOverdue = dueDate.Date < localDate,
                DueDate = dueDate.Date,
                BusinessDaysToDue = NotificationScheduleCalculator.BusinessDaysDifference(localDate, dueDate.Date)
            });
        }

        return result;
    }

    public async Task GenerateScheduledNotificationsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var normalizedUtcNow = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var sourceCandidates = await LoadSourceCandidatesAsync(context, cancellationToken);
        var totalCandidates = sourceCandidates.Count;
        var resolvedUserIds = await ResolveExistingUserIdsAsync(context, sourceCandidates, cancellationToken);

        var skippedInvalidUser = 0;
        var validSourceCandidates = new List<NotificationSourceCandidate>(sourceCandidates.Count);
        foreach (var candidate in sourceCandidates)
        {
            if (!TryNormalizeUserId(candidate.UserId, out var normalizedUserId) || !resolvedUserIds.Contains(normalizedUserId))
            {
                skippedInvalidUser++;
                logger.LogWarning(
                    "Skipping notification source {Kind}/{SourceId} because user '{UserId}' is invalid or missing.",
                    candidate.Kind,
                    candidate.SourceId,
                    candidate.UserId);
                continue;
            }

            validSourceCandidates.Add(candidate with { UserId = normalizedUserId });
        }

        var activeSourceKeys = validSourceCandidates
            .Select(candidate => candidate.SourceKey)
            .ToHashSet();

        var staleMarkedCount = await MarkStaleUnreadNotificationsAsync(
            context,
            activeSourceKeys,
            normalizedUtcNow,
            cancellationToken);

        if (staleMarkedCount > 0)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Failed to persist stale notification updates.");
                context.ChangeTracker.Clear();
            }
        }

        if (validSourceCandidates.Count == 0)
        {
            logger.LogInformation(
                "Notification scheduler run summary: candidates total={CandidatesTotal}, skipped invalid user={SkippedInvalidUser}, skipped weekend={SkippedWeekend}, skipped before 08:00={SkippedBeforeEight}, skipped pre-window={SkippedPreWindow}, created={CreatedCount}, created onboarding todo={CreatedTodoCount}.",
                totalCandidates,
                skippedInvalidUser,
                0,
                0,
                0,
                0,
                0);
            return;
        }

        var userTimeZones = await ResolveUserTimeZonesAsync(
            context,
            validSourceCandidates.Select(candidate => candidate.UserId).Distinct().ToList(),
            cancellationToken);

        var skippedWeekend = 0;
        var skippedBeforeEight = 0;
        var skippedPreWindow = 0;

        var dueCandidates = new List<DueNotificationCandidate>();
        foreach (var source in validSourceCandidates)
        {
            if (!userTimeZones.TryGetValue(source.UserId, out var userTimeZone))
            {
                userTimeZone = TimeZoneInfo.Utc;
            }

            var localNow = ConvertUtcToLocal(normalizedUtcNow, userTimeZone);
            var localDate = localNow.Date;
            if (!NotificationScheduleCalculator.IsBusinessDay(localDate))
            {
                skippedWeekend++;
                continue;
            }

            var firstReminderDate = NotificationScheduleCalculator.GetFirstReminderDate(source.DueDate);
            if (localDate < firstReminderDate)
            {
                skippedPreWindow++;
                continue;
            }

            if (localNow.TimeOfDay < ReminderTriggerLocalTime)
            {
                skippedBeforeEight++;
                continue;
            }

            dueCandidates.Add(new DueNotificationCandidate(source, localDate));
        }

        var createdCount = 0;
        var createdTodoCount = 0;
        var saveConflictCount = 0;
        var groupedCandidates = dueCandidates
            .GroupBy(candidate => new { candidate.Source.UserId, candidate.TriggerDate })
            .ToList();

        foreach (var group in groupedCandidates)
        {
            var existingSourceKeysForDay = await context.AppNotifications
                .AsNoTracking()
                .Where(notification =>
                    notification.UserId == group.Key.UserId
                    && notification.TriggerDate == group.Key.TriggerDate)
                .Select(notification => new { notification.Kind, notification.SourceId })
                .ToListAsync(cancellationToken);

            var existingSet = existingSourceKeysForDay
                .Select(entry => (entry.Kind, entry.SourceId))
                .ToHashSet();

            foreach (var candidate in group)
            {
                var dedupeKey = (candidate.Source.Kind, candidate.Source.SourceId);
                if (!existingSet.Add(dedupeKey))
                {
                    continue;
                }

                var notification = new AppNotification
                {
                    UserId = candidate.Source.UserId,
                    Kind = candidate.Source.Kind,
                    SourceId = candidate.Source.SourceId,
                    DueDate = candidate.Source.DueDate.Date,
                    TriggerDate = candidate.TriggerDate.Date,
                    CreatedAtUtc = normalizedUtcNow,
                    IsRead = false,
                    ReadAtUtc = null,
                    Title = candidate.Source.Title,
                    Body = candidate.Source.Body,
                    TargetUrl = candidate.Source.TargetUrl
                };

                context.AppNotifications.Add(notification);
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                    createdCount++;
                    if (candidate.Source.Kind == NotificationKind.OnboardingTodoUpcoming)
                    {
                        createdTodoCount++;
                    }
                }
                catch (DbUpdateException ex)
                {
                    saveConflictCount++;
                    context.Entry(notification).State = EntityState.Detached;
                    logger.LogWarning(
                        ex,
                        "Notification candidate save failed for user {UserId}, kind {Kind}, source {SourceId}, trigger {TriggerDate}.",
                        candidate.Source.UserId,
                        candidate.Source.Kind,
                        candidate.Source.SourceId,
                        candidate.TriggerDate);
                }
                catch (Exception ex)
                {
                    saveConflictCount++;
                    context.Entry(notification).State = EntityState.Detached;
                    logger.LogError(
                        ex,
                        "Notification candidate save crashed for user {UserId}, kind {Kind}, source {SourceId}, trigger {TriggerDate}.",
                        candidate.Source.UserId,
                        candidate.Source.Kind,
                        candidate.Source.SourceId,
                        candidate.TriggerDate);
                }
            }
        }

        logger.LogInformation(
            "Notification scheduler run summary: candidates total={CandidatesTotal}, skipped invalid user={SkippedInvalidUser}, skipped weekend={SkippedWeekend}, skipped before 08:00={SkippedBeforeEight}, skipped pre-window={SkippedPreWindow}, created={CreatedCount}, created onboarding todo={CreatedTodoCount}, save conflicts={SaveConflictCount}.",
            totalCandidates,
            skippedInvalidUser,
            skippedWeekend,
            skippedBeforeEight,
            skippedPreWindow,
            createdCount,
            createdTodoCount,
            saveConflictCount);
    }

    private async Task<List<NotificationSourceCandidate>> LoadSourceCandidatesAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var candidates = new List<NotificationSourceCandidate>();
        var reviewDefinitionId = await context.WikiAttributeDefinitions
            .AsNoTracking()
            .Where(definition => definition.Name == "ReviewDate")
            .Select(definition => definition.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (reviewDefinitionId > 0)
        {
            var wikiSources = await (
                from value in context.WikiPageAttributeValues.AsNoTracking()
                join page in context.WikiPages.AsNoTracking() on value.WikiPageId equals page.Id
                where value.AttributeDefinitionId == reviewDefinitionId
                    && page.OwnerId != null
                select new
                {
                    UserId = page.OwnerId!,
                    PageId = page.Id,
                    PageTitle = page.Title,
                    ReviewDateRaw = value.Value
                })
                .ToListAsync(cancellationToken);

            foreach (var source in wikiSources)
            {
                if (!NotificationDateParser.TryParseToDate(source.ReviewDateRaw, out var dueDate))
                {
                    if (!string.IsNullOrWhiteSpace(source.ReviewDateRaw))
                    {
                        logger.LogWarning(
                            "Invalid ReviewDate '{ReviewDateRaw}' for WikiPageId={WikiPageId}.",
                            source.ReviewDateRaw,
                            source.PageId);
                    }

                    continue;
                }

                candidates.Add(new NotificationSourceCandidate(
                    source.UserId,
                    NotificationKind.WikiReviewDate,
                    source.PageId,
                    dueDate.Date,
                    $"Review faellig: {source.PageTitle}",
                    $"Der Review-Termin ist am {dueDate:dd.MM.yyyy}.",
                    $"/wiki/view/{source.PageId}"));
            }
        }

        var onboardingSources = await context.OnboardingProfiles
            .AsNoTracking()
            .Where(profile => profile.AssignedAgentUserId != null && (profile.StartDate != null || profile.TargetDate != null))
            .Select(profile => new
            {
                ProfileId = profile.Id,
                UserId = profile.AssignedAgentUserId!,
                profile.FirstName,
                profile.LastName,
                profile.FullName,
                profile.StartDate,
                profile.TargetDate
            })
            .ToListAsync(cancellationToken);

        var onboardingProfileIds = onboardingSources
            .Select(profile => profile.ProfileId)
            .Distinct()
            .ToList();

        var openChecklistCountByProfileId = onboardingProfileIds.Count == 0
            ? new Dictionary<int, int>()
            : await context.OnboardingChecklistEntries
                .AsNoTracking()
                .Where(entry => onboardingProfileIds.Contains(entry.ProfileId) && !entry.IsCompleted)
                .GroupBy(entry => entry.ProfileId)
                .Select(group => new { ProfileId = group.Key, OpenCount = group.Count() })
                .ToDictionaryAsync(entry => entry.ProfileId, entry => entry.OpenCount, cancellationToken);

        var openMeasureCountByProfileId = onboardingProfileIds.Count == 0
            ? new Dictionary<int, int>()
            : await context.OnboardingMeasureEntries
                .AsNoTracking()
                .Where(entry => onboardingProfileIds.Contains(entry.ProfileId) && !entry.IsCompleted)
                .GroupBy(entry => entry.ProfileId)
                .Select(group => new { ProfileId = group.Key, OpenCount = group.Count() })
                .ToDictionaryAsync(entry => entry.ProfileId, entry => entry.OpenCount, cancellationToken);

        foreach (var profile in onboardingSources)
        {
            var displayName = ResolveOnboardingProfileDisplayName(profile.FirstName, profile.LastName, profile.FullName);
            if (profile.StartDate.HasValue)
            {
                var dueDate = profile.StartDate.Value.Date;
                candidates.Add(new NotificationSourceCandidate(
                    profile.UserId,
                    NotificationKind.OnboardingStartDate,
                    profile.ProfileId,
                    dueDate,
                    $"Onboarding Startdatum: {displayName}",
                    $"Das Startdatum ist am {dueDate:dd.MM.yyyy}.",
                    $"/reportdesigner/details/{profile.ProfileId}"));
            }

            if (profile.TargetDate.HasValue)
            {
                var dueDate = profile.TargetDate.Value.Date;
                candidates.Add(new NotificationSourceCandidate(
                    profile.UserId,
                    NotificationKind.OnboardingTargetDate,
                    profile.ProfileId,
                    dueDate,
                    $"Onboarding Zieldatum: {displayName}",
                    $"Das Zieldatum ist am {dueDate:dd.MM.yyyy}.",
                    $"/reportdesigner/details/{profile.ProfileId}"));
            }

            var openChecklistCount = openChecklistCountByProfileId.GetValueOrDefault(profile.ProfileId);
            var openMeasureCount = openMeasureCountByProfileId.GetValueOrDefault(profile.ProfileId);
            var openTodoCount = openChecklistCount + openMeasureCount;
            var todoDueDate = profile.TargetDate?.Date ?? profile.StartDate?.Date;

            if (openTodoCount > 0 && todoDueDate.HasValue)
            {
                var dueDate = todoDueDate.Value;
                candidates.Add(new NotificationSourceCandidate(
                    profile.UserId,
                    NotificationKind.OnboardingTodoUpcoming,
                    profile.ProfileId,
                    dueDate,
                    $"Onboarding Todos faellig: {displayName}",
                    $"Offene Todos bis {dueDate:dd.MM.yyyy}: {openChecklistCount} Checklist, {openMeasureCount} Measures.",
                    $"/reportdesigner/details/{profile.ProfileId}"));
            }
        }

        return candidates;
    }

    private static bool TryNormalizeUserId(string? rawUserId, out string normalizedUserId)
    {
        normalizedUserId = string.Empty;
        if (string.IsNullOrWhiteSpace(rawUserId))
        {
            return false;
        }

        normalizedUserId = rawUserId.Trim();
        return normalizedUserId.Length > 0;
    }

    private async Task<HashSet<string>> ResolveExistingUserIdsAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<NotificationSourceCandidate> sourceCandidates,
        CancellationToken cancellationToken)
    {
        var requestedUserIds = sourceCandidates
            .Select(candidate => candidate.UserId)
            .Where(userId => TryNormalizeUserId(userId, out _))
            .Select(userId => userId.Trim())
            .Distinct()
            .ToList();

        if (requestedUserIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var existingUsers = await context.Users
            .AsNoTracking()
            .Where(user => requestedUserIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        return existingUsers.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<int> MarkStaleUnreadNotificationsAsync(
        ApplicationDbContext context,
        HashSet<NotificationSourceKey> activeSourceKeys,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var unreadNotifications = await context.AppNotifications
            .Where(notification =>
                !notification.IsRead
                && ManagedNotificationKinds.Contains(notification.Kind))
            .ToListAsync(cancellationToken);

        var markedCount = 0;
        foreach (var notification in unreadNotifications)
        {
            var notificationKey = new NotificationSourceKey(
                notification.UserId,
                notification.Kind,
                notification.SourceId,
                notification.DueDate.Date);

            if (activeSourceKeys.Contains(notificationKey))
            {
                continue;
            }

            notification.IsRead = true;
            notification.ReadAtUtc = utcNow;
            markedCount++;
        }

        return markedCount;
    }

    private async Task<TimeZoneInfo> ResolveUserTimeZoneAsync(
        ApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

        return ResolveTimeZoneInfo(userId, timeZoneId);
    }

    private async Task<Dictionary<string, TimeZoneInfo>> ResolveUserTimeZonesAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, TimeZoneInfo>();
        }

        var userTimeZones = await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.TimeZone })
            .ToListAsync(cancellationToken);

        return userTimeZones
            .ToDictionary(
                entry => entry.Id,
                entry => ResolveTimeZoneInfo(entry.Id, entry.TimeZone));
    }

    private TimeZoneInfo ResolveTimeZoneInfo(string userId, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        var normalizedTimeZoneId = timeZoneId.Trim();

        if (TryFindTimeZoneInfo(normalizedTimeZoneId, out var resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalizedTimeZoneId, out var windowsTimeZoneId)
            && TryFindTimeZoneInfo(windowsTimeZoneId, out resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalizedTimeZoneId, out var ianaTimeZoneId)
            && TryFindTimeZoneInfo(ianaTimeZoneId, out resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        logger.LogWarning(
            "Unknown or invalid time zone '{TimeZoneId}' for user {UserId}. Falling back to UTC.",
            normalizedTimeZoneId,
            userId);

        return TimeZoneInfo.Utc;
    }

    private static bool TryFindTimeZoneInfo(string timeZoneId, out TimeZoneInfo timeZoneInfo)
    {
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZoneInfo = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZoneInfo = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static DateTime ConvertUtcToLocal(DateTime utcNow, TimeZoneInfo timeZoneInfo)
    {
        var normalized = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(normalized, timeZoneInfo);
    }

    private static string ResolveOnboardingProfileDisplayName(string? firstName, string? lastName, string? fullName)
    {
        var normalizedFirstName = firstName?.Trim();
        var normalizedLastName = lastName?.Trim();
        var fromSplitName = string.Join(
            " ",
            new[] { normalizedFirstName, normalizedLastName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fromSplitName))
        {
            return fromSplitName;
        }

        return string.IsNullOrWhiteSpace(fullName)
            ? "Unbekannt"
            : fullName.Trim();
    }

    private readonly record struct NotificationSourceKey(
        string UserId,
        NotificationKind Kind,
        int SourceId,
        DateTime DueDate);

    private sealed record NotificationSourceCandidate(
        string UserId,
        NotificationKind Kind,
        int SourceId,
        DateTime DueDate,
        string Title,
        string Body,
        string TargetUrl)
    {
        public NotificationSourceKey SourceKey => new(UserId, Kind, SourceId, DueDate.Date);
    }

    private sealed record DueNotificationCandidate(
        NotificationSourceCandidate Source,
        DateTime TriggerDate);
}
