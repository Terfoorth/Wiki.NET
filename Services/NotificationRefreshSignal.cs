namespace Wiki_Blaze.Services;

public sealed class NotificationRefreshSignal
{
    public event Action? Requested;

    public void RequestRefresh()
    {
        Requested?.Invoke();
    }
}
