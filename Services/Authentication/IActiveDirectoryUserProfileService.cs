using System.Security.Claims;

namespace Wiki_Blaze.Services.Authentication;

public interface IActiveDirectoryUserProfileService
{
    ActiveDirectoryUserProfile ResolveProfile(
        ClaimsPrincipal principal,
        string samAccountName,
        string? userPrincipalName,
        string allowedDomain);
}
