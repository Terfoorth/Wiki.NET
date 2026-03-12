namespace Wiki_Blaze.Services.Authentication;

public sealed class WindowsAuthenticationOptions
{
    public const string SectionName = "Authentication:Windows";

    public bool Enabled { get; set; }

    public string AllowedDomain { get; set; } = "HARMONIE.com";

    public bool AutoProvision { get; set; } = true;

    public WindowsProfileSyncMode ProfileSyncMode { get; set; } = WindowsProfileSyncMode.EveryLogin;

    public WindowsDirectoryServicesOptions DirectoryServices { get; set; } = new();
}

public enum WindowsProfileSyncMode
{
    FirstLoginOnly = 0,
    EveryLogin = 1,
}

public sealed class WindowsDirectoryServicesOptions
{
    public bool Enabled { get; set; } = true;

    public string? DomainController { get; set; }

    public string? Container { get; set; }
}
