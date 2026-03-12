using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using System.Runtime.Versioning;

namespace Wiki_Blaze.Services.Authentication;

public sealed class ActiveDirectoryUserProfileService(ILogger<ActiveDirectoryUserProfileService> logger) : IActiveDirectoryUserProfileService
{
    public Task<ActiveDirectoryUserProfile> ResolveProfileAsync(
        ClaimsPrincipal principal,
        string samAccountName,
        string? userPrincipalName,
        string allowedDomain,
        WindowsDirectoryServicesOptions directoryServicesOptions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var claimsProfile = BuildClaimsProfile(principal, samAccountName, userPrincipalName, allowedDomain);
        if (!directoryServicesOptions.Enabled)
        {
            return Task.FromResult(claimsProfile);
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogInformation(
                "DirectoryServices profile lookup is enabled but the current runtime is not Windows. Falling back to Kerberos claims for user {SamAccountName}.",
                samAccountName);
            return Task.FromResult(claimsProfile);
        }

        try
        {
            var directoryProfile = ResolveDirectoryProfile(
                samAccountName,
                userPrincipalName,
                allowedDomain,
                directoryServicesOptions);

            if (directoryProfile is null)
            {
                logger.LogWarning(
                    "DirectoryServices could not resolve a profile for {SamAccountName}. Falling back to Kerberos claims.",
                    samAccountName);
                return Task.FromResult(claimsProfile);
            }

            return Task.FromResult(MergeProfiles(directoryProfile, claimsProfile));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "DirectoryServices profile lookup failed for {SamAccountName}. Falling back to Kerberos claims.",
                samAccountName);
            return Task.FromResult(claimsProfile);
        }
    }

    [SupportedOSPlatform("windows")]
    private static ActiveDirectoryUserProfile? ResolveDirectoryProfile(
        string samAccountName,
        string? userPrincipalName,
        string allowedDomain,
        WindowsDirectoryServicesOptions directoryServicesOptions)
    {
        using var principalContext = CreatePrincipalContext(allowedDomain, directoryServicesOptions);
        using var userPrincipal = FindDirectoryUser(principalContext, samAccountName, userPrincipalName);
        if (userPrincipal is null)
        {
            return null;
        }

        var profile = new ActiveDirectoryUserProfile
        {
            SamAccountName = NormalizeOptional(userPrincipal.SamAccountName),
            UserPrincipalName = NormalizeOptional(userPrincipal.UserPrincipalName),
            DisplayName = NormalizeOptional(userPrincipal.DisplayName),
            GivenName = NormalizeOptional(userPrincipal.GivenName),
            Surname = NormalizeOptional(userPrincipal.Surname),
            Email = NormalizeOptional(userPrincipal.EmailAddress),
            LandlineNumber = NormalizeOptional(userPrincipal.VoiceTelephoneNumber),
        };

        if (userPrincipal.GetUnderlyingObject() is DirectoryEntry directoryEntry)
        {
            ApplyDirectoryEntryValues(profile, directoryEntry);
        }

        return profile;
    }

    [SupportedOSPlatform("windows")]
    private static PrincipalContext CreatePrincipalContext(
        string allowedDomain,
        WindowsDirectoryServicesOptions directoryServicesOptions)
    {
        var contextName = NormalizeOptional(directoryServicesOptions.DomainController)
            ?? NormalizeOptional(allowedDomain);
        var container = NormalizeOptional(directoryServicesOptions.Container);

        if (contextName is not null && container is not null)
        {
            return new PrincipalContext(ContextType.Domain, contextName, container);
        }

        if (contextName is not null)
        {
            return new PrincipalContext(ContextType.Domain, contextName);
        }

        return new PrincipalContext(ContextType.Domain);
    }

    [SupportedOSPlatform("windows")]
    private static UserPrincipal? FindDirectoryUser(
        PrincipalContext principalContext,
        string samAccountName,
        string? userPrincipalName)
    {
        var normalizedUserPrincipalName = NormalizeOptional(userPrincipalName);
        if (normalizedUserPrincipalName is not null)
        {
            var byUpn = UserPrincipal.FindByIdentity(
                principalContext,
                IdentityType.UserPrincipalName,
                normalizedUserPrincipalName);
            if (byUpn is not null)
            {
                return byUpn;
            }
        }

        var normalizedSamAccountName = NormalizeOptional(samAccountName);
        if (normalizedSamAccountName is null)
        {
            return null;
        }

        return UserPrincipal.FindByIdentity(
            principalContext,
            IdentityType.SamAccountName,
            normalizedSamAccountName);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyDirectoryEntryValues(ActiveDirectoryUserProfile profile, DirectoryEntry directoryEntry)
    {
        profile.SamAccountName ??= ReadDirectoryEntryValue(directoryEntry, "sAMAccountName");
        profile.UserPrincipalName ??= ReadDirectoryEntryValue(directoryEntry, "userPrincipalName");
        profile.DisplayName ??= ReadDirectoryEntryValue(directoryEntry, "displayName");
        profile.GivenName ??= ReadDirectoryEntryValue(directoryEntry, "givenName");
        profile.Surname ??= ReadDirectoryEntryValue(directoryEntry, "sn");
        profile.Email ??= ReadDirectoryEntryValue(directoryEntry, "mail");
        profile.Department ??= ReadDirectoryEntryValue(directoryEntry, "department");
        profile.JobTitle ??= ReadDirectoryEntryValue(directoryEntry, "title");
        profile.Location ??= ReadDirectoryEntryValue(directoryEntry, "physicalDeliveryOfficeName")
            ?? ReadDirectoryEntryValue(directoryEntry, "l");
        profile.LandlineNumber ??= ReadDirectoryEntryValue(directoryEntry, "telephoneNumber");
        profile.Country ??= ReadDirectoryEntryValue(directoryEntry, "co")
            ?? ReadDirectoryEntryValue(directoryEntry, "c");
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadDirectoryEntryValue(DirectoryEntry directoryEntry, string propertyName)
    {
        if (!directoryEntry.Properties.Contains(propertyName))
        {
            return null;
        }

        return NormalizeOptional(directoryEntry.Properties[propertyName]?.Value?.ToString());
    }

    private static ActiveDirectoryUserProfile BuildClaimsProfile(
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

    private static ActiveDirectoryUserProfile MergeProfiles(
        ActiveDirectoryUserProfile primaryProfile,
        ActiveDirectoryUserProfile fallbackProfile)
    {
        return new ActiveDirectoryUserProfile
        {
            SamAccountName = primaryProfile.SamAccountName ?? fallbackProfile.SamAccountName,
            UserPrincipalName = primaryProfile.UserPrincipalName ?? fallbackProfile.UserPrincipalName,
            DisplayName = primaryProfile.DisplayName ?? fallbackProfile.DisplayName,
            GivenName = primaryProfile.GivenName ?? fallbackProfile.GivenName,
            Surname = primaryProfile.Surname ?? fallbackProfile.Surname,
            Email = primaryProfile.Email ?? fallbackProfile.Email,
            Department = primaryProfile.Department ?? fallbackProfile.Department,
            JobTitle = primaryProfile.JobTitle ?? fallbackProfile.JobTitle,
            Location = primaryProfile.Location ?? fallbackProfile.Location,
            LandlineNumber = primaryProfile.LandlineNumber ?? fallbackProfile.LandlineNumber,
            Country = primaryProfile.Country ?? fallbackProfile.Country,
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
