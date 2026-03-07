using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public interface INotificationService
{
    Task<NotificationInboxDto> GetInboxAsync(
        string userId,
        int take = 20,
        bool includeRead = true,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(string userId, int notificationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WikiReviewAlertStateDto>> GetWikiReviewAlertStatesAsync(
        string userId,
        IEnumerable<int> wikiPageIds,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationInboxDto
{
    public IReadOnlyList<NotificationInboxItemDto> Items { get; init; } = Array.Empty<NotificationInboxItemDto>();
    public int UnreadCount { get; init; }
}

public sealed class NotificationInboxItemDto
{
    public int Id { get; init; }
    public NotificationKind Kind { get; init; }
    public int SourceId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string TargetUrl { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public DateTime TriggerDate { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public bool IsRead { get; init; }
}

public sealed class WikiReviewAlertStateDto
{
    public int PageId { get; init; }
    public bool IsAlert { get; init; }
    public bool IsOverdue { get; init; }
    public DateTime DueDate { get; init; }
    public int BusinessDaysToDue { get; init; }
}
