namespace Wiki_Blaze.Services.Authentication;

public sealed class WindowsAuthenticationOptions
{
    public const string SectionName = "Authentication:Windows";

    public bool Enabled { get; set; }

    public string AllowedDomain { get; set; } = "HARMONIE.com";

    public bool AutoProvision { get; set; } = true;

    public WindowsProfileSyncMode ProfileSyncMode { get; set; } = WindowsProfileSyncMode.FirstLoginOnly;
}

public enum WindowsProfileSyncMode
{
    FirstLoginOnly = 0,
    EveryLogin = 1,
}
