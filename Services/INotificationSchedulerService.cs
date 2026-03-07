namespace Wiki_Blaze.Services;

public interface INotificationSchedulerService
{
    Task GenerateScheduledNotificationsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
