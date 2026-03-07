namespace Wiki_Blaze.Services;

public interface IReminderSettingsAdminService
{
    Task<ReminderAdminSettingsView> GetAsync(string adminUserId, CancellationToken cancellationToken = default);

    Task SaveAsync(
        string adminUserId,
        ReminderAdminSettingsUpdate update,
        CancellationToken cancellationToken = default);
}

public sealed class ReminderAdminSettingsView
{
    public bool Enabled { get; set; }
    public int SendHourLocal { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public bool PasswordConfigured { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
}

public sealed class ReminderAdminSettingsUpdate
{
    public bool Enabled { get; set; }
    public int SendHourLocal { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? NewPassword { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
}
