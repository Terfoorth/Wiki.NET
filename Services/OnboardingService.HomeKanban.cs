using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;
using Wiki_Blaze.Services.Notifications;

namespace Wiki_Blaze.Services;

public partial class OnboardingService
{
    private static readonly Regex HomeMentionRegex = new(@"@\w+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const int MaxMentionsPerComment = 10;
    private static readonly HashSet<string> AllowedHomeCommentImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif",
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public async Task<HomeKanbanBoardDto> GetHomeKanbanBoardAsync(string userId, int takePerColumn = 25, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        takePerColumn = Math.Clamp(takePerColumn, 5, 100);

        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);

        var profiles = await context.OnboardingProfiles
            .AsNoTracking()
            .Include(profile => profile.AssignedAgentUser)
            .Include(profile => profile.LinkedUser)
            .Include(profile => profile.CreatedByUser)
            .OrderByDescending(profile => profile.LastModifiedAt)
            .ToListAsync(cancellationToken);

        var localDate = await ResolveHomeLocalDateAsync(context, userId, cancellationToken);

        var cards = new List<HomeKanbanCardDto>(profiles.Count);
        cards.AddRange(BuildCardsForStatus(profiles, OnboardingProfileStatus.Draft, HomeKanbanColumnKeys.OnboardingDrafts, takePerColumn, localDate));
        cards.AddRange(BuildCardsForStatus(profiles, OnboardingProfileStatus.NotStarted, HomeKanbanColumnKeys.OnboardingNotStarted, takePerColumn, localDate));
        cards.AddRange(BuildCardsForStatus(profiles, OnboardingProfileStatus.InProgress, HomeKanbanColumnKeys.OnboardingInProgress, takePerColumn, localDate));
        cards.AddRange(BuildCardsForStatus(profiles, OnboardingProfileStatus.Completed, HomeKanbanColumnKeys.OnboardingCompleted, takePerColumn, localDate));
        cards.AddRange(BuildCardsForStatus(profiles, OnboardingProfileStatus.Archived, HomeKanbanColumnKeys.OnboardingArchived, takePerColumn, localDate));

        var defaultColumns = GetDefaultOnboardingColumns();

        var cardStates = await context.HomeKanbanCardStates
            .AsNoTracking()
            .Where(state =>
                state.UserId == userId
                && state.ViewType == HomeKanbanViewType.Onboarding
                && state.EntityType == HomeKanbanCardEntityType.OnboardingProfile)
            .ToListAsync(cancellationToken);

        var stateByEntryId = cardStates.ToDictionary(state => state.EntryId);
        foreach (var card in cards)
        {
            if (!stateByEntryId.TryGetValue(card.EntryId, out var state))
            {
                continue;
            }

            if (state.SortOrder > 0 && string.Equals(state.ColumnKey, card.ColumnKey, StringComparison.OrdinalIgnoreCase))
            {
                card.SortOrder = state.SortOrder;
            }
        }

        NormalizeOnboardingCardOrder(cards, defaultColumns);

        var commentCounts = cards.Count == 0
            ? new Dictionary<int, int>()
            : await context.HomeEntryComments
                .AsNoTracking()
                .Where(comment => comment.Scope == HomeCommentScope.Onboarding && cards.Select(card => card.EntryId).Contains(comment.EntryId))
                .GroupBy(comment => comment.EntryId)
                .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

        foreach (var card in cards)
        {
            card.CommentCount = commentCounts.GetValueOrDefault(card.EntryId);
        }

        var columns = await BuildOrderedColumnsAsync(context, userId, HomeKanbanViewType.Onboarding, defaultColumns, cancellationToken);
        return new HomeKanbanBoardDto
        {
            ViewType = HomeKanbanViewType.Onboarding,
            Columns = columns,
            Cards = cards
        };
    }

    public async Task<bool> MoveHomeKanbanCardAsync(string userId, MoveCardRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ViewType != HomeKanbanViewType.Onboarding || request.EntityType != HomeKanbanCardEntityType.OnboardingProfile)
        {
            return false;
        }

        var board = await GetHomeKanbanBoardAsync(userId, takePerColumn: 100, cancellationToken);
        var card = board.Cards.FirstOrDefault(entry => entry.EntryId == request.EntryId && entry.EntityType == HomeKanbanCardEntityType.OnboardingProfile);
        if (card is null)
        {
            return false;
        }

        if (!CanMoveOnboardingCard(card.ColumnKey, request.TargetColumnKey))
        {
            return false;
        }

        var validColumns = board.Columns.Select(column => column.Key).ToHashSet();
        if (!validColumns.Contains(request.TargetColumnKey))
        {
            return false;
        }

        var targetStatus = MapColumnKeyToStatus(request.TargetColumnKey);
        if (targetStatus is null)
        {
            return false;
        }

        var cards = board.Cards.ToList();
        cards.Remove(card);
        card.ColumnKey = request.TargetColumnKey;

        var targetCards = cards
            .Where(entry => entry.ColumnKey == request.TargetColumnKey)
            .OrderBy(entry => entry.SortOrder)
            .ThenByDescending(entry => entry.LastActivityUtc)
            .ToList();

        var targetIndex = Math.Clamp(request.TargetIndex, 0, targetCards.Count);
        targetCards.Insert(targetIndex, card);

        var orderedByColumn = new Dictionary<string, List<HomeKanbanCardDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in board.Columns.OrderBy(column => column.SortOrder))
        {
            orderedByColumn[column.Key] = column.Key == request.TargetColumnKey
                ? targetCards
                : cards.Where(entry => entry.ColumnKey == column.Key)
                    .OrderBy(entry => entry.SortOrder)
                    .ThenByDescending(entry => entry.LastActivityUtc)
                    .ToList();
        }

        var flattened = new List<HomeKanbanCardDto>(cards.Count + 1);
        foreach (var column in board.Columns.OrderBy(column => column.SortOrder))
        {
            var columnCards = orderedByColumn[column.Key];
            for (var index = 0; index < columnCards.Count; index++)
            {
                columnCards[index].SortOrder = (index + 1) * 10;
                flattened.Add(columnCards[index]);
            }
        }

        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.OnboardingProfiles
            .FirstOrDefaultAsync(entry => entry.Id == request.EntryId, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        if (profile.Status == OnboardingProfileStatus.Draft || targetStatus == OnboardingProfileStatus.Draft)
        {
            return false;
        }

        if (profile.Status != targetStatus.Value)
        {
            profile.Status = targetStatus.Value;
            profile.LastModifiedAt = DateTime.UtcNow;
        }

        await PersistCardStatesAsync(context, userId, HomeKanbanViewType.Onboarding, HomeKanbanCardEntityType.OnboardingProfile, flattened, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveHomeKanbanColumnOrderAsync(string userId, ReorderColumnsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ViewType != HomeKanbanViewType.Onboarding)
        {
            return;
        }

        var defaults = GetDefaultOnboardingColumns();
        var validKeys = defaults.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderedKeys = request.OrderedColumnKeys
            .Where(key => validKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var fallback in defaults.Select(column => column.Key))
        {
            if (!orderedKeys.Contains(fallback, StringComparer.OrdinalIgnoreCase))
            {
                orderedKeys.Add(fallback);
            }
        }

        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        var states = await context.HomeKanbanColumnStates
            .Where(state => state.UserId == userId && state.ViewType == HomeKanbanViewType.Onboarding)
            .ToListAsync(cancellationToken);

        var stateByKey = states.ToDictionary(state => state.ColumnKey, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < orderedKeys.Count; index++)
        {
            var key = orderedKeys[index];
            if (!stateByKey.TryGetValue(key, out var state))
            {
                state = new HomeKanbanColumnState
                {
                    UserId = userId,
                    ViewType = HomeKanbanViewType.Onboarding,
                    ColumnKey = key
                };
                context.HomeKanbanColumnStates.Add(state);
            }

            state.SortOrder = (index + 1) * 10;
            state.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OnboardingQuickDetailDto?> GetQuickDetailAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.OnboardingProfiles
            .AsNoTracking()
            .Include(entry => entry.AssignedAgentUser)
            .Include(entry => entry.MeasureEntries)
                .ThenInclude(entry => entry.CatalogItem)
            .Include(entry => entry.ChecklistEntries)
                .ThenInclude(entry => entry.CatalogItem)
            .FirstOrDefaultAsync(entry => entry.Id == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var openMeasures = profile.MeasureEntries
            .Where(entry => !entry.IsCompleted)
            .OrderBy(entry => entry.CatalogItem?.SortOrder ?? int.MaxValue)
            .ThenBy(entry => entry.CreatedAt)
            .ToList();

        var openChecklist = profile.ChecklistEntries
            .Where(entry => !entry.IsCompleted)
            .OrderBy(entry => entry.CatalogItem?.SortOrder ?? int.MaxValue)
            .ThenBy(entry => entry.CreatedAt)
            .ToList();

        return new OnboardingQuickDetailDto
        {
            ProfileId = profile.Id,
            FullName = BuildProfileDisplayName(profile.FirstName, profile.LastName, profile.FullName),
            Department = profile.Department,
            JobTitle = profile.JobTitle,
            AssignedAgentDisplayName = profile.AssignedAgentUser is null
                ? null
                : ResolveUserDisplayName(
                    profile.AssignedAgentUser.DisplayName,
                    profile.AssignedAgentUser.FirstName,
                    profile.AssignedAgentUser.LastName,
                    profile.AssignedAgentUser.UserName,
                    profile.AssignedAgentUser.Email),
            Status = profile.Status,
            MeasureOpenCount = openMeasures.Count,
            MeasureTotalCount = profile.MeasureEntries.Count,
            ChecklistOpenCount = openChecklist.Count,
            ChecklistTotalCount = profile.ChecklistEntries.Count,
            OpenMeasureItems = openMeasures.Select(entry => entry.CatalogItem?.Name ?? entry.Value).Take(8).ToList(),
            OpenChecklistItems = openChecklist.Select(entry => entry.CatalogItem?.Name ?? entry.Id.ToString()).Take(8).ToList()
        };
    }

    public async Task<List<HomeEntryCommentDto>> GetHomeCommentsAsync(int profileId, string? userId, CancellationToken cancellationToken = default)
    {
        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.OnboardingProfiles.AsNoTracking().AnyAsync(entry => entry.Id == profileId, cancellationToken))
        {
            return new List<HomeEntryCommentDto>();
        }

        var comments = await context.HomeEntryComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Attachments)
            .Where(comment => comment.Scope == HomeCommentScope.Onboarding && comment.EntryId == profileId)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return comments.Select(MapHomeCommentToDto).ToList();
    }

    public async Task<HomeEntryCommentDto> AddHomeCommentAsync(string? userId, CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Scope != HomeCommentScope.Onboarding)
        {
            throw new InvalidOperationException("Ungültiger Kommentarbereich.");
        }

        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        var profileExists = await context.OnboardingProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.Id == request.EntryId, cancellationToken);

        if (!profileExists)
        {
            throw new InvalidOperationException("Onboarding-Eintrag wurde nicht gefunden.");
        }

        var normalizedText = request.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText) && request.Attachments.Count == 0)
        {
            throw new InvalidOperationException("Kommentartext oder mindestens ein Anhang ist erforderlich.");
        }

        var mentions = ClampHomeMentionPayload(BuildHomeMentionTokens(normalizedText, request.Mentions));
        var nowUtc = DateTime.UtcNow;
        var entity = new HomeEntryComment
        {
            Scope = HomeCommentScope.Onboarding,
            EntryId = request.EntryId,
            AuthorId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            Text = normalizedText,
            MentionTokensJson = mentions.Count == 0 ? null : JsonSerializer.Serialize(mentions),
            CreatedAtUtc = nowUtc
        };

        foreach (var attachment in request.Attachments)
        {
            if (attachment.Content.Length == 0)
            {
                continue;
            }

            if (!AllowedHomeCommentImageTypes.Contains(attachment.ContentType))
            {
                continue;
            }

            entity.Attachments.Add(new HomeEntryCommentAttachment
            {
                FileName = LimitHomeAttachmentFileName(attachment.FileName),
                ContentType = attachment.ContentType,
                Content = attachment.Content.ToArray(),
                SizeBytes = attachment.Content.LongLength,
                UploadedAtUtc = nowUtc
            });
        }

        context.HomeEntryComments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await CreateOnboardingCommentNotificationsAsync(context, request.EntryId, entity.Id, entity.AuthorId, mentions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.HomeEntryComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Attachments)
            .FirstAsync(comment => comment.Id == entity.Id, cancellationToken);

        return MapHomeCommentToDto(saved);
    }

    public async Task DeleteHomeCommentAsync(string? userId, int commentId, CancellationToken cancellationToken = default)
    {
        await using var context = await DbFactory.CreateDbContextAsync(cancellationToken);
        var comment = await context.HomeEntryComments
            .FirstOrDefaultAsync(entry => entry.Id == commentId && entry.Scope == HomeCommentScope.Onboarding, cancellationToken);

        if (comment is null)
        {
            return;
        }

        if (!string.Equals(comment.AuthorId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Nur der Autor darf den Kommentar löschen.");
        }

        await DeleteCommentNotificationsAsync(context, comment.Id, cancellationToken);
        context.HomeEntryComments.Remove(comment);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task DeleteCommentNotificationsAsync(
        ApplicationDbContext context,
        int commentId,
        CancellationToken cancellationToken)
    {
        var notifications = await context.AppNotifications
            .Where(notification =>
                notification.SourceId == commentId
                && (notification.Kind == NotificationKind.HomeCommentOwner
                    || notification.Kind == NotificationKind.HomeCommentMention))
            .ToListAsync(cancellationToken);

        if (notifications.Count > 0)
        {
            context.AppNotifications.RemoveRange(notifications);
        }
    }

    private static List<HomeKanbanCardDto> BuildCardsForStatus(
        List<OnboardingProfile> profiles,
        OnboardingProfileStatus status,
        string columnKey,
        int takePerColumn,
        DateTime localDate)
    {
        return profiles
            .Where(profile => profile.Status == status)
            .OrderByDescending(profile => profile.LastModifiedAt)
            .Take(takePerColumn)
            .Select((profile, index) =>
            {
                var reminderState = BuildOnboardingReminderState(profile.TargetDate, localDate);
                return new HomeKanbanCardDto
                {
                    EntryId = profile.Id,
                    EntityType = HomeKanbanCardEntityType.OnboardingProfile,
                    ColumnKey = columnKey,
                    SortOrder = (index + 1) * 10,
                    Title = BuildProfileDisplayName(profile.FirstName, profile.LastName, profile.FullName),
                    CreatorDisplayName = profile.CreatedByUser is null
                        ? "-"
                        : ResolveUserDisplayName(
                            profile.CreatedByUser.DisplayName,
                            profile.CreatedByUser.FirstName,
                            profile.CreatedByUser.LastName,
                            profile.CreatedByUser.UserName,
                            profile.CreatedByUser.Email),
                    OwnerDisplayName = profile.AssignedAgentUser is null
                        ? "-"
                        : ResolveUserDisplayName(
                            profile.AssignedAgentUser.DisplayName,
                            profile.AssignedAgentUser.FirstName,
                            profile.AssignedAgentUser.LastName,
                            profile.AssignedAgentUser.UserName,
                            profile.AssignedAgentUser.Email),
                    CategoryOrRole = !string.IsNullOrWhiteSpace(profile.JobTitle) ? profile.JobTitle! : "-",
                    Subtitle = profile.Department,
                    PrimaryLinkText = "Details",
                    PrimaryLinkUrl = $"/reportdesigner/details/{profile.Id}",
                    LastActivityUtc = profile.LastModifiedAt,
                    IsReminderActive = reminderState.IsActive,
                    IsReminderOverdue = reminderState.IsOverdue,
                    ReminderDueDate = reminderState.DueDate,
                    ReminderLabel = reminderState.Label,
                    ReminderKind = reminderState.IsActive ? HomeKanbanReminderKind.OnboardingTargetDate : null
                };
            })
            .ToList();
    }

    private static List<HomeKanbanColumnDto> GetDefaultOnboardingColumns()
    {
        return
        [
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.OnboardingDrafts,
                Title = "Entwürfe",
                SortOrder = 10,
                CardsDraggable = false,
                AcceptDrop = false
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.OnboardingNotStarted,
                Title = "Noch nicht gestartet",
                SortOrder = 20,
                CardsDraggable = true,
                AcceptDrop = true
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.OnboardingInProgress,
                Title = "In Bearbeitung",
                SortOrder = 30,
                CardsDraggable = true,
                AcceptDrop = true
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.OnboardingCompleted,
                Title = "Abgeschlossen",
                SortOrder = 40,
                CardsDraggable = true,
                AcceptDrop = true
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.OnboardingArchived,
                Title = "Archiviert",
                SortOrder = 50,
                CardsDraggable = true,
                AcceptDrop = true
            }
        ];
    }

    private static HomeReminderState BuildOnboardingReminderState(DateTime? dueDate, DateTime localDate)
    {
        if (!dueDate.HasValue)
        {
            return HomeReminderState.None;
        }

        var normalizedDueDate = dueDate.Value.Date;
        var firstReminderDate = NotificationScheduleCalculator.GetFirstReminderDate(normalizedDueDate);
        if (localDate < firstReminderDate)
        {
            return HomeReminderState.None;
        }

        var isOverdue = normalizedDueDate < localDate;
        var label = isOverdue
            ? $"Onboarding-Zieldatum ueberfaellig seit {normalizedDueDate:dd.MM.yyyy}"
            : $"Onboarding-Zieldatum faellig am {normalizedDueDate:dd.MM.yyyy}";

        return new HomeReminderState(true, isOverdue, normalizedDueDate, label);
    }

    private static async Task<DateTime> ResolveHomeLocalDateAsync(
        ApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ResolveHomeTimeZoneInfo(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    private static TimeZoneInfo ResolveHomeTimeZoneInfo(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        var normalizedTimeZoneId = timeZoneId.Trim();
        if (TryFindHomeTimeZoneInfo(normalizedTimeZoneId, out var resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalizedTimeZoneId, out var windowsTimeZoneId)
            && TryFindHomeTimeZoneInfo(windowsTimeZoneId, out resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalizedTimeZoneId, out var ianaTimeZoneId)
            && TryFindHomeTimeZoneInfo(ianaTimeZoneId, out resolvedTimeZone))
        {
            return resolvedTimeZone;
        }

        return TimeZoneInfo.Utc;
    }

    private static bool TryFindHomeTimeZoneInfo(string timeZoneId, out TimeZoneInfo timeZoneInfo)
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

    private static bool CanMoveOnboardingCard(string sourceColumnKey, string targetColumnKey)
    {
        if (string.Equals(sourceColumnKey, HomeKanbanColumnKeys.OnboardingDrafts, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(targetColumnKey, HomeKanbanColumnKeys.OnboardingDrafts, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static OnboardingProfileStatus? MapColumnKeyToStatus(string columnKey)
    {
        if (string.Equals(columnKey, HomeKanbanColumnKeys.OnboardingDrafts, StringComparison.OrdinalIgnoreCase))
        {
            return OnboardingProfileStatus.Draft;
        }

        if (string.Equals(columnKey, HomeKanbanColumnKeys.OnboardingNotStarted, StringComparison.OrdinalIgnoreCase))
        {
            return OnboardingProfileStatus.NotStarted;
        }

        if (string.Equals(columnKey, HomeKanbanColumnKeys.OnboardingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            return OnboardingProfileStatus.InProgress;
        }

        if (string.Equals(columnKey, HomeKanbanColumnKeys.OnboardingCompleted, StringComparison.OrdinalIgnoreCase))
        {
            return OnboardingProfileStatus.Completed;
        }

        if (string.Equals(columnKey, HomeKanbanColumnKeys.OnboardingArchived, StringComparison.OrdinalIgnoreCase))
        {
            return OnboardingProfileStatus.Archived;
        }

        return null;
    }

    private static void NormalizeOnboardingCardOrder(List<HomeKanbanCardDto> cards, IReadOnlyList<HomeKanbanColumnDto> columns)
    {
        foreach (var column in columns.OrderBy(entry => entry.SortOrder))
        {
            var columnCards = cards
                .Where(entry => string.Equals(entry.ColumnKey, column.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.SortOrder)
                .ThenByDescending(entry => entry.LastActivityUtc)
                .ToList();

            for (var index = 0; index < columnCards.Count; index++)
            {
                columnCards[index].SortOrder = (index + 1) * 10;
            }
        }
    }

    private static async Task<List<HomeKanbanColumnDto>> BuildOrderedColumnsAsync(
        ApplicationDbContext context,
        string userId,
        HomeKanbanViewType viewType,
        List<HomeKanbanColumnDto> defaults,
        CancellationToken cancellationToken)
    {
        var states = await context.HomeKanbanColumnStates
            .AsNoTracking()
            .Where(state => state.UserId == userId && state.ViewType == viewType)
            .ToListAsync(cancellationToken);

        var orderByKey = states.ToDictionary(state => state.ColumnKey, state => state.SortOrder, StringComparer.OrdinalIgnoreCase);
        foreach (var column in defaults)
        {
            if (orderByKey.TryGetValue(column.Key, out var sortOrder))
            {
                column.SortOrder = sortOrder;
            }
        }

        return defaults
            .OrderBy(column => column.SortOrder)
            .ToList();
    }

    private static async Task PersistCardStatesAsync(
        ApplicationDbContext context,
        string userId,
        HomeKanbanViewType viewType,
        HomeKanbanCardEntityType entityType,
        IReadOnlyList<HomeKanbanCardDto> cards,
        CancellationToken cancellationToken)
    {
        var states = await context.HomeKanbanCardStates
            .Where(state =>
                state.UserId == userId
                && state.ViewType == viewType
                && state.EntityType == entityType)
            .ToListAsync(cancellationToken);

        var stateByEntry = states.ToDictionary(state => state.EntryId);
        foreach (var card in cards)
        {
            if (!stateByEntry.TryGetValue(card.EntryId, out var state))
            {
                state = new HomeKanbanCardState
                {
                    UserId = userId,
                    ViewType = viewType,
                    EntityType = entityType,
                    EntryId = card.EntryId
                };
                context.HomeKanbanCardStates.Add(state);
            }

            state.ColumnKey = card.ColumnKey;
            state.SortOrder = card.SortOrder;
            state.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static List<MentionToken> BuildHomeMentionTokens(string text, IReadOnlyList<CommentMentionInputDto>? mentionInputs)
    {
        var structuredMentions = NormalizeHomeStructuredMentions(mentionInputs);
        return structuredMentions.Count > 0
            ? structuredMentions
            : ExtractHomeMentionTokens(text);
    }

    private static List<MentionToken> NormalizeHomeStructuredMentions(IReadOnlyList<CommentMentionInputDto>? mentionInputs)
    {
        if (mentionInputs is null || mentionInputs.Count == 0)
        {
            return new List<MentionToken>();
        }

        var mentions = new List<MentionToken>(Math.Min(mentionInputs.Count, MaxMentionsPerComment));
        var seenUserIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mention in mentionInputs)
        {
            if (!TryNormalizeHomeUserId(mention.UserId, out var userId))
            {
                continue;
            }

            if (!seenUserIds.Add(userId))
            {
                continue;
            }

            var displayText = NormalizeHomeText(mention.DisplayName) ?? userId;
            var email = NormalizeHomeText(mention.Email);
            mentions.Add(new MentionToken
            {
                Type = MentionType.User,
                Token = $"@{displayText}",
                ReferenceId = userId,
                DisplayText = displayText,
                TargetUrl = string.IsNullOrWhiteSpace(email) ? null : $"mailto:{email}"
            });

            if (mentions.Count >= MaxMentionsPerComment)
            {
                break;
            }
        }

        return mentions;
    }

    private static List<MentionToken> ClampHomeMentionPayload(IReadOnlyList<MentionToken> mentions)
    {
        if (mentions.Count == 0)
        {
            return new List<MentionToken>();
        }

        var boundedMentions = mentions.ToList();
        while (boundedMentions.Count > 0)
        {
            if (JsonSerializer.Serialize(boundedMentions).Length <= 2000)
            {
                return boundedMentions;
            }

            boundedMentions.RemoveAt(boundedMentions.Count - 1);
        }

        return new List<MentionToken>();
    }

    private static List<MentionToken> ExtractHomeMentionTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<MentionToken>();
        }

        var mentions = new List<MentionToken>();
        foreach (Match match in HomeMentionRegex.Matches(text))
        {
            var token = match.Value;
            var type = token.StartsWith("@USER", StringComparison.OrdinalIgnoreCase)
                ? MentionType.User
                : token.StartsWith("@ENTRY", StringComparison.OrdinalIgnoreCase)
                    ? MentionType.Entry
                    : MentionType.Unknown;

            mentions.Add(new MentionToken
            {
                Type = type,
                Token = token,
                DisplayText = token
            });
        }

        return mentions;
    }

    private async Task CreateOnboardingCommentNotificationsAsync(
        ApplicationDbContext context,
        int entryId,
        int commentId,
        string? authorId,
        IReadOnlyList<MentionToken> mentions,
        CancellationToken cancellationToken)
    {
        var profileInfo = await context.OnboardingProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == entryId)
            .Select(profile => new
            {
                profile.FirstName,
                profile.LastName,
                profile.FullName,
                profile.CreatedByUserId,
                profile.AssignedAgentUserId,
                profile.LinkedUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profileInfo is null)
        {
            return;
        }

        var recipientKinds = new Dictionary<string, NotificationKind>(StringComparer.Ordinal);
        var ownerCandidate = TryNormalizeHomeUserId(profileInfo.CreatedByUserId, out var createdByUserId)
            ? createdByUserId
            : TryNormalizeHomeUserId(profileInfo.AssignedAgentUserId, out var assignedAgentUserId)
                ? assignedAgentUserId
                : TryNormalizeHomeUserId(profileInfo.LinkedUserId, out var linkedUserId)
                    ? linkedUserId
                    : null;

        if (!string.IsNullOrWhiteSpace(ownerCandidate) && !IsSameHomeUser(ownerCandidate, authorId))
        {
            recipientKinds[ownerCandidate] = NotificationKind.HomeCommentOwner;
        }

        foreach (var mentionUserId in mentions
                     .Where(mention => mention.Type == MentionType.User && TryNormalizeHomeUserId(mention.ReferenceId, out _))
                     .Select(mention => mention.ReferenceId!.Trim())
                     .Distinct(StringComparer.Ordinal))
        {
            if (IsSameHomeUser(mentionUserId, authorId))
            {
                continue;
            }

            recipientKinds[mentionUserId] = NotificationKind.HomeCommentMention;
        }

        if (recipientKinds.Count == 0)
        {
            return;
        }

        var recipientUserIds = recipientKinds.Keys.ToList();
        var recipients = await context.Users
            .AsNoTracking()
            .Where(user => recipientUserIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            return;
        }

        var authorDisplayName = await ResolveHomeCommentAuthorDisplayNameAsync(context, authorId, cancellationToken);
        var createdAtUtc = DateTime.UtcNow;
        var targetUrl = $"/reportdesigner/details/{entryId}";
        var profileDisplayName = BuildProfileDisplayName(profileInfo.FirstName, profileInfo.LastName, profileInfo.FullName);
        if (string.IsNullOrWhiteSpace(profileDisplayName))
        {
            profileDisplayName = $"Onboarding #{entryId}";
        }

        foreach (var recipientUserId in recipients)
        {
            var kind = recipientKinds[recipientUserId];
            var title = kind == NotificationKind.HomeCommentMention
                ? $"Erwaehnung in Kommentar: {profileDisplayName}"
                : $"Neuer Kommentar: {profileDisplayName}";
            var body = kind == NotificationKind.HomeCommentMention
                ? $"{authorDisplayName} hat dich in einem Kommentar erwaehnt."
                : $"{authorDisplayName} hat einen neuen Kommentar hinterlassen.";

            context.AppNotifications.Add(new AppNotification
            {
                UserId = recipientUserId,
                Kind = kind,
                SourceId = commentId,
                DueDate = createdAtUtc.Date,
                TriggerDate = createdAtUtc.Date,
                CreatedAtUtc = createdAtUtc,
                IsRead = false,
                ReadAtUtc = null,
                Title = title,
                Body = body,
                TargetUrl = targetUrl
            });
        }
    }

    private async Task<string> ResolveHomeCommentAuthorDisplayNameAsync(
        ApplicationDbContext context,
        string? authorId,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeHomeUserId(authorId, out var normalizedAuthorId))
        {
            return "Jemand";
        }

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == normalizedAuthorId, cancellationToken);

        return user is null
            ? "Jemand"
            : ResolveUserDisplayName(user.DisplayName, user.FirstName, user.LastName, user.UserName, user.Email);
    }

    private static bool IsSameHomeUser(string? leftUserId, string? rightUserId)
    {
        return TryNormalizeHomeUserId(leftUserId, out var left)
               && TryNormalizeHomeUserId(rightUserId, out var right)
               && string.Equals(left, right, StringComparison.Ordinal);
    }

    private static bool TryNormalizeHomeUserId(string? userId, out string normalizedUserId)
    {
        normalizedUserId = string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        normalizedUserId = userId.Trim();
        return normalizedUserId.Length > 0;
    }

    private static string? NormalizeHomeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static List<MentionToken> ReadHomeMentionTokens(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new List<MentionToken>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<MentionToken>>(payload) ?? new List<MentionToken>();
        }
        catch
        {
            return new List<MentionToken>();
        }
    }

    private static string LimitHomeAttachmentFileName(string fileName)
    {
        var value = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "attachment";
        }

        const int maxLength = 260;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private readonly record struct HomeReminderState(
        bool IsActive,
        bool IsOverdue,
        DateTime? DueDate,
        string? Label)
    {
        public static HomeReminderState None => new(false, false, null, null);
    }

    private static HomeEntryCommentDto MapHomeCommentToDto(HomeEntryComment comment)
    {
        return new HomeEntryCommentDto
        {
            Id = comment.Id,
            Scope = comment.Scope,
            EntryId = comment.EntryId,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAtUtc,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = comment.Author is null
                ? "-"
                : ResolveUserDisplayName(
                    comment.Author.DisplayName,
                    comment.Author.FirstName,
                    comment.Author.LastName,
                    comment.Author.UserName,
                    comment.Author.Email),
            Mentions = ReadHomeMentionTokens(comment.MentionTokensJson),
            Attachments = comment.Attachments
                .OrderBy(attachment => attachment.UploadedAtUtc)
                .Select(attachment => new CommentAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    SizeBytes = attachment.SizeBytes,
                    UploadedAtUtc = attachment.UploadedAtUtc,
                    Content = attachment.Content
                })
                .ToList()
        };
    }
}

