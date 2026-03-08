using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public partial class WikiService
{
    private static readonly Regex MentionRegex = new(@"@\w+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedCommentImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif",
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public async Task<HomeKanbanBoardDto> GetHomeKanbanBoardAsync(string userId, int takePerColumn = 25)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        takePerColumn = Math.Clamp(takePerColumn, 5, 100);

        using var context = await _dbFactory.CreateDbContextAsync();

        var defaultColumns = GetDefaultWikiColumns();
        var visibleQuery = ApplyVisibilityFilter(BuildPageQuery(context), context, userId)
            .Where(page => page.EntryType == WikiEntryType.Standard);

        var drafts = await visibleQuery
            .Where(page => page.OwnerId == userId && page.Status == WikiPageStatus.Draft)
            .OrderByDescending(page => page.LastModified)
            .Take(takePerColumn)
            .ToListAsync();

        var recentViewEvents = await context.WikiEntryViewEvents
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .GroupBy(entry => entry.WikiPageId)
            .Select(group => new
            {
                PageId = group.Key,
                LastViewedAt = group.Max(item => item.ViewedAtUtc)
            })
            .OrderByDescending(entry => entry.LastViewedAt)
            .Take(takePerColumn * 3)
            .ToListAsync();

        var recentViewIds = recentViewEvents.Select(entry => entry.PageId).ToHashSet();
        var recentViewedPages = recentViewIds.Count == 0
            ? new List<WikiPage>()
            : await visibleQuery.Where(page => recentViewIds.Contains(page.Id)).ToListAsync();

        var templateUsageEvents = await context.WikiTemplateUsageEvents
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .GroupBy(entry => entry.WikiPageId)
            .Select(group => new
            {
                PageId = group.Key,
                UsageCount = group.Count(),
                LastUsedAt = group.Max(item => item.UsedAtUtc)
            })
            .OrderByDescending(entry => entry.UsageCount)
            .ThenByDescending(entry => entry.LastUsedAt)
            .Take(takePerColumn * 3)
            .ToListAsync();

        var templateIds = templateUsageEvents.Select(entry => entry.PageId).ToHashSet();
        var templatePages = templateIds.Count == 0
            ? new List<WikiPage>()
            : await visibleQuery
                .Where(page => templateIds.Contains(page.Id) && page.Status == WikiPageStatus.Template)
                .ToListAsync();

        var favoriteUsageEvents = await context.WikiFavoriteUsageEvents
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .GroupBy(entry => entry.WikiPageId)
            .Select(group => new
            {
                PageId = group.Key,
                UsageCount = group.Count(),
                LastUsedAt = group.Max(item => item.UsedAtUtc)
            })
            .OrderByDescending(entry => entry.UsageCount)
            .ThenByDescending(entry => entry.LastUsedAt)
            .Take(takePerColumn * 3)
            .ToListAsync();

        var favoriteIds = favoriteUsageEvents.Select(entry => entry.PageId).ToHashSet();
        var favoritePages = favoriteIds.Count == 0
            ? new List<WikiPage>()
            : await visibleQuery
                .Where(page => favoriteIds.Contains(page.Id))
                .ToListAsync();

        var allPages = drafts
            .Concat(recentViewedPages)
            .Concat(templatePages)
            .Concat(favoritePages)
            .GroupBy(page => page.Id)
            .Select(group => group.First())
            .ToList();

        await MapWikiPageUserDisplayNamesAsync(context, allPages);

        var pageById = allPages.ToDictionary(page => page.Id);

        var cards = new List<HomeKanbanCardDto>(allPages.Count);
        cards.AddRange(
            drafts.Select((page, index) =>
                BuildWikiCard(page, HomeKanbanColumnKeys.WikiDrafts, (index + 1) * 10)));

        cards.AddRange(
            recentViewEvents
                .Where(entry => pageById.ContainsKey(entry.PageId))
                .Take(takePerColumn)
                .Select((entry, index) =>
                    BuildWikiCard(pageById[entry.PageId], HomeKanbanColumnKeys.WikiLastViews, (index + 1) * 10, entry.LastViewedAt)));

        cards.AddRange(
            templateUsageEvents
                .Where(entry => pageById.ContainsKey(entry.PageId))
                .Take(takePerColumn)
                .Select((entry, index) =>
                    BuildWikiCard(pageById[entry.PageId], HomeKanbanColumnKeys.WikiTemplateUsage, (index + 1) * 10, entry.LastUsedAt)));

        cards.AddRange(
            favoriteUsageEvents
                .Where(entry => pageById.ContainsKey(entry.PageId))
                .Take(takePerColumn)
                .Select((entry, index) =>
                    BuildWikiCard(pageById[entry.PageId], HomeKanbanColumnKeys.WikiFavoriteUsage, (index + 1) * 10, entry.LastUsedAt)));

        var cardStates = await context.HomeKanbanCardStates
            .AsNoTracking()
            .Where(state =>
                state.UserId == userId
                && state.ViewType == HomeKanbanViewType.Wiki
                && state.EntityType == HomeKanbanCardEntityType.WikiEntry)
            .ToListAsync();

        var stateByEntryId = cardStates.ToDictionary(state => state.EntryId);
        var validColumns = defaultColumns.Select(column => column.Key).ToHashSet();
        foreach (var card in cards)
        {
            if (!stateByEntryId.TryGetValue(card.EntryId, out var state))
            {
                continue;
            }

            if (validColumns.Contains(state.ColumnKey))
            {
                card.ColumnKey = state.ColumnKey;
            }

            card.SortOrder = state.SortOrder;
        }

        NormalizeCardOrder(cards, defaultColumns);

        var commentCounts = cards.Count == 0
            ? new Dictionary<int, int>()
            : await context.HomeEntryComments
                .AsNoTracking()
                .Where(comment => comment.Scope == HomeCommentScope.Wiki && cards.Select(card => card.EntryId).Contains(comment.EntryId))
                .GroupBy(comment => comment.EntryId)
                .ToDictionaryAsync(group => group.Key, group => group.Count());

        foreach (var card in cards)
        {
            card.CommentCount = commentCounts.GetValueOrDefault(card.EntryId);
        }

        var columns = await BuildOrderedColumnsAsync(context, userId, HomeKanbanViewType.Wiki, defaultColumns);
        return new HomeKanbanBoardDto
        {
            ViewType = HomeKanbanViewType.Wiki,
            Columns = columns,
            Cards = cards
        };
    }

    public async Task<bool> MoveHomeKanbanCardAsync(string userId, MoveCardRequest request)
    {
        if (request.ViewType != HomeKanbanViewType.Wiki || request.EntityType != HomeKanbanCardEntityType.WikiEntry)
        {
            return false;
        }

        var board = await GetHomeKanbanBoardAsync(userId, takePerColumn: 100);
        var card = board.Cards.FirstOrDefault(entry => entry.EntryId == request.EntryId && entry.EntityType == HomeKanbanCardEntityType.WikiEntry);
        if (card is null)
        {
            return false;
        }

        if (!CanMoveWikiCard(card.ColumnKey, request.TargetColumnKey))
        {
            return false;
        }

        var validColumns = board.Columns.Select(column => column.Key).ToHashSet();
        if (!validColumns.Contains(request.TargetColumnKey))
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

        using var context = await _dbFactory.CreateDbContextAsync();
        await PersistCardStatesAsync(context, userId, HomeKanbanViewType.Wiki, HomeKanbanCardEntityType.WikiEntry, flattened);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task SaveHomeKanbanColumnOrderAsync(string userId, ReorderColumnsRequest request)
    {
        if (request.ViewType != HomeKanbanViewType.Wiki)
        {
            return;
        }

        var defaultColumns = GetDefaultWikiColumns();
        var validKeys = defaultColumns.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderedKeys = request.OrderedColumnKeys
            .Where(key => validKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var fallback in defaultColumns.Select(column => column.Key))
        {
            if (!orderedKeys.Contains(fallback, StringComparer.OrdinalIgnoreCase))
            {
                orderedKeys.Add(fallback);
            }
        }

        using var context = await _dbFactory.CreateDbContextAsync();
        var states = await context.HomeKanbanColumnStates
            .Where(state => state.UserId == userId && state.ViewType == HomeKanbanViewType.Wiki)
            .ToListAsync();

        var stateByKey = states.ToDictionary(state => state.ColumnKey, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < orderedKeys.Count; index++)
        {
            var key = orderedKeys[index];
            if (!stateByKey.TryGetValue(key, out var state))
            {
                state = new HomeKanbanColumnState
                {
                    UserId = userId,
                    ViewType = HomeKanbanViewType.Wiki,
                    ColumnKey = key
                };
                context.HomeKanbanColumnStates.Add(state);
            }

            state.SortOrder = (index + 1) * 10;
            state.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<HomeEntryCommentDto>> GetHomeCommentsAsync(HomeCommentScope scope, int entryId, string? userId)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        if (!await CanReadCommentScopeAsync(context, scope, entryId, userId))
        {
            return new List<HomeEntryCommentDto>();
        }

        var comments = await context.HomeEntryComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Attachments)
            .Where(comment => comment.Scope == scope && comment.EntryId == entryId)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .Take(200)
            .ToListAsync();

        return comments
            .Select(MapCommentToDto)
            .ToList();
    }

    public async Task<HomeEntryCommentDto> AddHomeCommentAsync(string? userId, CreateCommentRequest request)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        if (!await CanReadCommentScopeAsync(context, request.Scope, request.EntryId, userId))
        {
            throw new InvalidOperationException("Keine Berechtigung für diesen Eintrag.");
        }

        var normalizedText = request.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText) && request.Attachments.Count == 0)
        {
            throw new InvalidOperationException("Kommentartext oder mindestens ein Anhang ist erforderlich.");
        }

        var mentions = ExtractMentionTokens(normalizedText);

        var entity = new HomeEntryComment
        {
            Scope = request.Scope,
            EntryId = request.EntryId,
            AuthorId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            Text = normalizedText,
            MentionTokensJson = mentions.Count == 0 ? null : JsonSerializer.Serialize(mentions),
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var attachment in request.Attachments)
        {
            if (attachment.Content.Length == 0)
            {
                continue;
            }

            if (!AllowedCommentImageTypes.Contains(attachment.ContentType))
            {
                continue;
            }

            entity.Attachments.Add(new HomeEntryCommentAttachment
            {
                FileName = LimitAttachmentFileName(attachment.FileName),
                ContentType = attachment.ContentType,
                Content = attachment.Content.ToArray(),
                SizeBytes = attachment.Content.LongLength,
                UploadedAtUtc = DateTime.UtcNow
            });
        }

        context.HomeEntryComments.Add(entity);
        await context.SaveChangesAsync();

        var saved = await context.HomeEntryComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Attachments)
            .FirstAsync(comment => comment.Id == entity.Id);

        return MapCommentToDto(saved);
    }

    public async Task DeleteHomeCommentAsync(string? userId, int commentId)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        var comment = await context.HomeEntryComments
            .FirstOrDefaultAsync(entry => entry.Id == commentId);

        if (comment is null)
        {
            return;
        }

        if (!string.Equals(comment.AuthorId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Nur der Autor darf den Kommentar löschen.");
        }

        context.HomeEntryComments.Remove(comment);
        await context.SaveChangesAsync();
    }

    public async Task RecordWikiEntryViewAsync(string? userId, int pageId)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        var exists = await context.WikiPages.AnyAsync(page => page.Id == pageId);
        if (!exists)
        {
            return;
        }

        context.WikiEntryViewEvents.Add(new WikiEntryViewEvent
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            WikiPageId = pageId,
            ViewedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task RecordTemplateUsageAsync(string? userId, int pageId)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        var exists = await context.WikiPages.AnyAsync(page => page.Id == pageId && page.Status == WikiPageStatus.Template);
        if (!exists)
        {
            return;
        }

        context.WikiTemplateUsageEvents.Add(new WikiTemplateUsageEvent
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            WikiPageId = pageId,
            UsedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task RecordFavoriteUsageAsync(string? userId, int pageId)
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        var exists = await context.WikiPages.AnyAsync(page => page.Id == pageId);
        if (!exists)
        {
            return;
        }

        context.WikiFavoriteUsageEvents.Add(new WikiFavoriteUsageEvent
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            WikiPageId = pageId,
            UsedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static List<HomeKanbanColumnDto> GetDefaultWikiColumns()
    {
        return
        [
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.WikiDrafts,
                Title = "Deine Entwürfe",
                SortOrder = 10,
                CardsDraggable = true,
                AcceptDrop = true
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.WikiLastViews,
                Title = "Deine letzten Wiki Views",
                SortOrder = 20,
                CardsDraggable = true,
                AcceptDrop = true
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.WikiTemplateUsage,
                Title = "Meist genutzte Vorlagen",
                SortOrder = 30,
                CardsDraggable = false,
                AcceptDrop = false
            },
            new HomeKanbanColumnDto
            {
                Key = HomeKanbanColumnKeys.WikiFavoriteUsage,
                Title = "Meist genutzte Favoriten",
                SortOrder = 40,
                CardsDraggable = false,
                AcceptDrop = false
            }
        ];
    }

    private static bool CanMoveWikiCard(string sourceColumnKey, string targetColumnKey)
    {
        if (string.Equals(sourceColumnKey, HomeKanbanColumnKeys.WikiTemplateUsage, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceColumnKey, HomeKanbanColumnKeys.WikiFavoriteUsage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(targetColumnKey, HomeKanbanColumnKeys.WikiTemplateUsage, StringComparison.OrdinalIgnoreCase)
            || string.Equals(targetColumnKey, HomeKanbanColumnKeys.WikiFavoriteUsage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static HomeKanbanCardDto BuildWikiCard(WikiPage page, string columnKey, int sortOrder, DateTime? activityOverride = null)
    {
        return new HomeKanbanCardDto
        {
            EntryId = page.Id,
            EntityType = HomeKanbanCardEntityType.WikiEntry,
            ColumnKey = columnKey,
            SortOrder = sortOrder,
            Title = page.Title,
            CreatorDisplayName = !string.IsNullOrWhiteSpace(page.AuthorDisplayName) ? page.AuthorDisplayName : "-",
            OwnerDisplayName = page.OwnerDisplayName,
            CategoryOrRole = page.Category?.Name ?? "-",
            Subtitle = page.Status == WikiPageStatus.Template ? "Vorlage" : null,
            PrimaryLinkText = "Eintrag Ansicht",
            PrimaryLinkUrl = $"/wiki/view/{page.Id}?returnUrl=%2F",
            LastActivityUtc = activityOverride ?? page.LastModified
        };
    }

    private static void NormalizeCardOrder(List<HomeKanbanCardDto> cards, IReadOnlyList<HomeKanbanColumnDto> columns)
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
        List<HomeKanbanColumnDto> defaults)
    {
        var states = await context.HomeKanbanColumnStates
            .AsNoTracking()
            .Where(state => state.UserId == userId && state.ViewType == viewType)
            .ToListAsync();

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
        IReadOnlyList<HomeKanbanCardDto> cards)
    {
        var states = await context.HomeKanbanCardStates
            .Where(state =>
                state.UserId == userId
                && state.ViewType == viewType
                && state.EntityType == entityType)
            .ToListAsync();

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

    private async Task<bool> CanReadCommentScopeAsync(ApplicationDbContext context, HomeCommentScope scope, int entryId, string? userId)
    {
        return scope switch
        {
            HomeCommentScope.Wiki => await ApplyVisibilityFilter(BuildPageQuery(context), context, userId)
                .AnyAsync(page => page.Id == entryId),
            HomeCommentScope.Onboarding => await context.OnboardingProfiles.AsNoTracking().AnyAsync(profile => profile.Id == entryId),
            _ => false
        };
    }

    private static List<MentionToken> ExtractMentionTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<MentionToken>();
        }

        var mentions = new List<MentionToken>();
        foreach (Match match in MentionRegex.Matches(text))
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
                Token = token
            });
        }

        return mentions;
    }

    private static List<MentionToken> ReadMentionTokens(string? payload)
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

    private static string LimitAttachmentFileName(string fileName)
    {
        var value = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "attachment";
        }

        const int maxLength = 260;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static HomeEntryCommentDto MapCommentToDto(HomeEntryComment comment)
    {
        return new HomeEntryCommentDto
        {
            Id = comment.Id,
            Scope = comment.Scope,
            EntryId = comment.EntryId,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAtUtc,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = ResolveDisplayName(comment.Author),
            Mentions = ReadMentionTokens(comment.MentionTokensJson),
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
