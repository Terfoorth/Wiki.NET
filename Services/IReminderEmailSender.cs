namespace Wiki_Blaze.Services;

public interface IReminderEmailSender
{
    Task SendAsync(ReminderEmailMessage message, CancellationToken cancellationToken = default);
}
