namespace Wiki_Blaze.Services.Authentication;

public sealed class ActiveDirectoryUserProfile
{
    public string? SamAccountName { get; set; }

    public string? UserPrincipalName { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public string? Department { get; set; }

    public string? JobTitle { get; set; }

    public string? Location { get; set; }

    public string? LandlineNumber { get; set; }

    public string? Country { get; set; }
}
