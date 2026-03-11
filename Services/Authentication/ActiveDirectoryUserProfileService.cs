using System.Security.Claims;

namespace Wiki_Blaze.Services.Authentication;

public sealed class ActiveDirectoryUserProfileService : IActiveDirectoryUserProfileService
{
    public ActiveDirectoryUserProfile ResolveProfile(
        ClaimsPrincipal principal,
        string samAccountName,
        string? userPrincipalName,
        string allowedDomain)
    {
        var fallbackUpn = NormalizeOptional(userPrincipalName)
            ?? NormalizeOptional(principal.FindFirstValue(ClaimTypes.Upn))
            ?? $"{samAccountName}@{NormalizeDomain(allowedDomain)}";

        var fallbackDisplayName = NormalizeOptional(principal.FindFirstValue("displayName"))
            ?? NormalizeOptional(principal.FindFirstValue(ClaimTypes.Name))
            ?? NormalizeOptional(samAccountName);

        return new ActiveDirectoryUserProfile
        {
            SamAccountName = NormalizeOptional(samAccountName),
            UserPrincipalName = NormalizeOptional(fallbackUpn),
            DisplayName = fallbackDisplayName,
            GivenName = NormalizeOptional(principal.FindFirstValue(ClaimTypes.GivenName)),
            Surname = NormalizeOptional(principal.FindFirstValue(ClaimTypes.Surname)),
            Email = NormalizeOptional(principal.FindFirstValue(ClaimTypes.Email))
                ?? NormalizeOptional(principal.FindFirstValue("mail"))
                ?? NormalizeOptional(fallbackUpn),
            Department = NormalizeOptional(principal.FindFirstValue("department")),
            JobTitle = NormalizeOptional(principal.FindFirstValue("title")),
            Location = NormalizeOptional(principal.FindFirstValue("physicalDeliveryOfficeName"))
                ?? NormalizeOptional(principal.FindFirstValue("l")),
            LandlineNumber = NormalizeOptional(principal.FindFirstValue("telephoneNumber")),
            Country = NormalizeOptional(principal.FindFirstValue("co"))
                ?? NormalizeOptional(principal.FindFirstValue("c")),
        };
    }

    private static string NormalizeDomain(string domain)
    {
        return NormalizeOptional(domain)?.ToLowerInvariant() ?? string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
