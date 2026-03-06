using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public interface INotificationService
{
    Task<IReadOnlyList<UserNotification>> RefreshAndGetAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(string userId, int notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}
