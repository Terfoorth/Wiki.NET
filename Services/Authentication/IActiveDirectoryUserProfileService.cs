using System.Security.Claims;

namespace Wiki_Blaze.Services.Authentication;

public interface IActiveDirectoryUserProfileService
{
    Task<ActiveDirectoryUserProfile> ResolveProfileAsync(
        ClaimsPrincipal principal,
        string samAccountName,
        string? userPrincipalName,
        string allowedDomain,
        WindowsDirectoryServicesOptions directoryServicesOptions,
        CancellationToken cancellationToken = default);
}
