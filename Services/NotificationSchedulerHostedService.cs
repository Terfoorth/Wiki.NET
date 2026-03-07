namespace Wiki_Blaze.Services;

public sealed class NotificationSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationSchedulerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SchedulerInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(SchedulerInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var schedulerService = scope.ServiceProvider.GetRequiredService<INotificationSchedulerService>();
            await schedulerService.GenerateScheduledNotificationsAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // App shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification scheduler run failed.");
        }
    }
}
