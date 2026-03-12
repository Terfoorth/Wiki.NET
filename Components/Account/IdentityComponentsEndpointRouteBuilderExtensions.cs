using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Wiki_Blaze.Components.Account;
using Wiki_Blaze.Components.Account.Pages;
using Wiki_Blaze.Components.Account.Pages.Manage;
using Wiki_Blaze.Data;
using Wiki_Blaze.Services;
using Wiki_Blaze.Services.Authentication;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        private const string WindowsLoginProvider = "Windows";
        private const string WindowsLoginDisplayName = "Windows Integrated";
        private const string WindowsAccountNameClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname";
        private const string WindowsLoginEntryRoute = "/LoginWithWindows";
        private const string WindowsLoginCallbackRoute = "/LoginWithWindowsCallback";
        private const string LegacyKerberosLoginRoute = "/LoginWithKerberos";
        private const string LegacyKerberosCallbackRoute = "/LoginWithKerberosCallback";
        private const string WindowsLoginEntryPath = "/Account/LoginWithWindows";
        private const string WindowsLoginCallbackPath = "/Account/LoginWithWindowsCallback";
        private const string ChallengeAttemptedQueryKey = "challengeAttempted";

        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return Results.Challenge(properties, [provider]);
            });

            accountGroup.MapGet(WindowsLoginEntryRoute, (
                HttpContext context,
                [FromQuery] string? returnUrl,
                [FromServices] IOptions<WindowsAuthenticationOptions> windowsAuthOptions,
                [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("WindowsLogin");
                return BeginWindowsLoginChallenge(context, returnUrl, windowsAuthOptions.Value, logger);
            });

            accountGroup.MapGet(LegacyKerberosLoginRoute, (
                HttpContext context,
                [FromQuery] string? returnUrl,
                [FromServices] ILoggerFactory loggerFactory) =>
            {
                var safeReturnUrl = NormalizeReturnUrl(returnUrl);
                var logger = loggerFactory.CreateLogger("WindowsLogin");
                logger.LogInformation("Legacy endpoint '{Path}' called. Redirecting to '{TargetPath}'.", LegacyKerberosLoginRoute, WindowsLoginEntryPath);
                return Results.LocalRedirect(BuildWindowsLoginRedirectUrl(safeReturnUrl));
            });

            accountGroup.MapGet(WindowsLoginCallbackRoute, async (
                HttpContext context,
                [FromQuery] string? returnUrl,
                [FromQuery] bool challengeAttempted,
                [FromServices] IOptions<WindowsAuthenticationOptions> windowsAuthOptions,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] IWikiFavoriteGroupService wikiFavoriteGroupService,
                [FromServices] IActiveDirectoryUserProfileService activeDirectoryUserProfileService,
                [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("WindowsLogin");
                return await HandleWindowsLoginCallbackAsync(
                    context,
                    returnUrl,
                    challengeAttempted,
                    windowsAuthOptions.Value,
                    signInManager,
                    userManager,
                    wikiFavoriteGroupService,
                    activeDirectoryUserProfileService,
                    logger);
            });

            accountGroup.MapGet(LegacyKerberosCallbackRoute, async (
                HttpContext context,
                [FromQuery] string? returnUrl,
                [FromQuery] bool challengeAttempted,
                [FromServices] IOptions<WindowsAuthenticationOptions> windowsAuthOptions,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] IWikiFavoriteGroupService wikiFavoriteGroupService,
                [FromServices] IActiveDirectoryUserProfileService activeDirectoryUserProfileService,
                [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("WindowsLogin");
                logger.LogInformation("Legacy endpoint '{Path}' called. Processing through '{TargetPath}'.", LegacyKerberosCallbackRoute, WindowsLoginCallbackRoute);
                return await HandleWindowsLoginCallbackAsync(
                    context,
                    returnUrl,
                    challengeAttempted,
                    windowsAuthOptions.Value,
                    signInManager,
                    userManager,
                    wikiFavoriteGroupService,
                    activeDirectoryUserProfileService,
                    logger);
            });

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                SignInManager<ApplicationUser> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return Results.LocalRedirect($"~/{returnUrl}");
            });

            accountGroup.MapGet("/Logout", async (
                ClaimsPrincipal user,
                SignInManager<ApplicationUser> signInManager
                ) =>
            {
                await signInManager.SignOutAsync();
                return Results.LocalRedirect($"~/");
            });

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return Results.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return Results.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }

        private static IResult BeginWindowsLoginChallenge(
            HttpContext context,
            string? returnUrl,
            WindowsAuthenticationOptions windowsOptions,
            ILogger logger)
        {
            var safeReturnUrl = NormalizeReturnUrl(returnUrl);
            if (!windowsOptions.Enabled)
            {
                SetStatusMessage(context, "Error: Windows login is currently disabled.");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            logger.LogInformation(
                "Starting Windows login challenge. ReturnUrl={ReturnUrl}, Scheme={Scheme}, Callback={CallbackPath}",
                safeReturnUrl,
                NegotiateDefaults.AuthenticationScheme,
                WindowsLoginCallbackPath);

            var redirectUrl = BuildWindowsCallbackUrl(context, safeReturnUrl, WindowsLoginCallbackPath, challengeAttempted: true);
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
            };

            return Results.Challenge(properties, [NegotiateDefaults.AuthenticationScheme]);
        }

        private static async Task<IResult> HandleWindowsLoginCallbackAsync(
            HttpContext context,
            string? returnUrl,
            bool challengeAttempted,
            WindowsAuthenticationOptions windowsOptions,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IWikiFavoriteGroupService wikiFavoriteGroupService,
            IActiveDirectoryUserProfileService activeDirectoryUserProfileService,
            ILogger logger)
        {
            var safeReturnUrl = NormalizeReturnUrl(returnUrl);
            if (!windowsOptions.Enabled)
            {
                SetStatusMessage(context, "Error: Windows login is currently disabled.");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            ClaimsPrincipal? principal = null;
            if (TryGetIntegratedWindowsPrincipal(context.User, out var contextPrincipal))
            {
                principal = contextPrincipal;
                logger.LogInformation(
                    "Using integrated Windows principal from HttpContext.User. IdentityName={IdentityName}, AuthType={AuthType}",
                    ResolveWindowsIdentityName(principal),
                    principal.Identity?.AuthenticationType);
            }

            if (principal is null)
            {
                var negotiateResult = await context.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
                if (negotiateResult.Succeeded && TryGetIntegratedWindowsPrincipal(negotiateResult.Principal, out var negotiatePrincipal))
                {
                    principal = negotiatePrincipal;
                    logger.LogInformation(
                        "Resolved Windows principal via '{Scheme}' authenticate fallback. IdentityName={IdentityName}, AuthType={AuthType}",
                        NegotiateDefaults.AuthenticationScheme,
                        ResolveWindowsIdentityName(principal),
                        principal.Identity?.AuthenticationType);
                }
            }

            if (principal is null)
            {
                if (!challengeAttempted)
                {
                    var challengeRedirect = BuildWindowsCallbackUrl(context, safeReturnUrl, WindowsLoginCallbackPath, challengeAttempted: true);
                    logger.LogInformation(
                        "No authenticated Windows principal found. Issuing single challenge with callback '{CallbackPath}'.",
                        WindowsLoginCallbackPath);
                    return Results.Challenge(
                        new AuthenticationProperties { RedirectUri = challengeRedirect },
                        [NegotiateDefaults.AuthenticationScheme]);
                }

                logger.LogWarning(
                    "Windows login failed after one challenge attempt. ReturnUrl={ReturnUrl}",
                    safeReturnUrl);
                SetStatusMessage(context, "Error: Windows authentication failed. Use local login or verify browser and IIS Windows authentication settings.");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            var rawIdentityName = ResolveWindowsIdentityName(principal);
            if (!TryParseWindowsIdentity(rawIdentityName, windowsOptions.AllowedDomain, out var identityInfo))
            {
                logger.LogWarning("Windows login denied due to domain mismatch. IdentityName={IdentityName}", rawIdentityName);
                var allowedDomainLabel = NormalizeOptional(windowsOptions.AllowedDomain) ?? "configured";
                SetStatusMessage(context, $"Error: Windows login is only allowed for {allowedDomainLabel} domain users.");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            var profile = await activeDirectoryUserProfileService.ResolveProfileAsync(
                principal,
                identityInfo.SamAccountName,
                identityInfo.UserPrincipalName,
                windowsOptions.AllowedDomain,
                windowsOptions.DirectoryServices,
                context.RequestAborted);

            ApplyIdentityFallbacks(profile, identityInfo, windowsOptions.AllowedDomain);

            var providerKey = ResolveProviderKey(principal, profile, identityInfo);
            var user = await FindUserForWindowsLoginAsync(userManager, providerKey, identityInfo, profile);
            var isNewUser = false;

            if (user is null)
            {
                if (!windowsOptions.AutoProvision)
                {
                    logger.LogWarning(
                        "No local account found for Windows identity {IdentityName} and auto provisioning is disabled.",
                        rawIdentityName);
                    SetStatusMessage(context, "Error: No local account found and automatic provisioning is disabled.");
                    return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
                }

                user = new ApplicationUser
                {
                    UserName = TrimToLength(profile.SamAccountName ?? identityInfo.SamAccountName, 256),
                    Email = TrimToLength(profile.Email, 256),
                    EmailConfirmed = true,
                };

                ApplyProfileToUser(user, profile, overwriteExisting: true);

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var createErrors = string.Join(", ", createResult.Errors.Select(error => error.Description));
                    logger.LogWarning(
                        "Windows auto-provisioning failed for {UserName}: {Errors}",
                        user.UserName,
                        createErrors);
                    SetStatusMessage(context, $"Error: Could not provision account ({createErrors}).");
                    return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
                }

                isNewUser = true;
            }
            else if (windowsOptions.ProfileSyncMode == WindowsProfileSyncMode.EveryLogin)
            {
                ApplyProfileToUser(user, profile, overwriteExisting: true);
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var updateErrors = string.Join(", ", updateResult.Errors.Select(error => error.Description));
                    logger.LogWarning(
                        "Windows profile synchronization failed for user {UserId}: {Errors}",
                        user.Id,
                        updateErrors);
                }
                else
                {
                    logger.LogInformation("Synchronized AD profile for user {UserId} on login.", user.Id);
                }
            }

            var addLoginResult = await EnsureWindowsLoginMappingAsync(userManager, user, providerKey);
            if (!addLoginResult.Succeeded)
            {
                var loginErrors = string.Join(", ", addLoginResult.Errors.Select(error => error.Description));
                logger.LogWarning(
                    "Could not attach Windows login mapping for user {UserId}: {Errors}",
                    user.Id,
                    loginErrors);
                SetStatusMessage(context, $"Error: Windows account link failed ({loginErrors}).");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            logger.LogInformation("Windows login mapping ensured for user {UserId}.", user.Id);

            var addRoleResult = await EnsureUserRoleAsync(userManager, user, "User");
            if (!addRoleResult.Succeeded)
            {
                var roleErrors = string.Join(", ", addRoleResult.Errors.Select(error => error.Description));
                logger.LogWarning(
                    "Could not ensure role 'User' for user {UserId}: {Errors}",
                    user.Id,
                    roleErrors);
                SetStatusMessage(context, $"Error: User role assignment failed ({roleErrors}).");
                return Results.LocalRedirect(BuildLoginRedirectUrl(safeReturnUrl));
            }

            await wikiFavoriteGroupService.EnsureDefaultGroupAsync(user.Id);

            if (isNewUser)
            {
                logger.LogInformation(
                    "Provisioned new user {UserId} from Windows identity {IdentityName}.",
                    user.Id,
                    rawIdentityName);
            }
            else
            {
                logger.LogInformation("Matched Windows identity {IdentityName} to existing local user {UserId}.", rawIdentityName, user.Id);
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            logger.LogInformation("Windows login succeeded for user {UserId}.", user.Id);
            return Results.LocalRedirect(BuildReturnRedirectUrl(safeReturnUrl));
        }

        private static string ResolveProviderKey(
            ClaimsPrincipal principal,
            ActiveDirectoryUserProfile profile,
            ParsedWindowsIdentity identityInfo)
        {
            var sid = principal.FindFirstValue(ClaimTypes.PrimarySid)
                ?? principal.FindFirstValue(ClaimTypes.Sid)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(sid))
            {
                return sid;
            }

            if (!string.IsNullOrWhiteSpace(profile.UserPrincipalName))
            {
                return profile.UserPrincipalName.ToUpperInvariant();
            }

            return $"{identityInfo.DomainToken}\\{identityInfo.SamAccountName}".ToUpperInvariant();
        }

        private static async Task<ApplicationUser?> FindUserForWindowsLoginAsync(
            UserManager<ApplicationUser> userManager,
            string providerKey,
            ParsedWindowsIdentity identityInfo,
            ActiveDirectoryUserProfile profile)
        {
            var user = await userManager.FindByLoginAsync(WindowsLoginProvider, providerKey);
            if (user is not null)
            {
                return user;
            }

            var candidateUserNames = new[]
            {
                profile.SamAccountName,
                identityInfo.SamAccountName,
                profile.UserPrincipalName,
                $"{identityInfo.DomainToken}\\{identityInfo.SamAccountName}",
            };

            foreach (var candidateUserName in candidateUserNames
                .Select(NormalizeOptional)
                .Where(value => value is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                user = await userManager.FindByNameAsync(candidateUserName!);
                if (user is not null)
                {
                    return user;
                }
            }

            var email = NormalizeOptional(profile.Email);
            if (email is not null)
            {
                user = await userManager.FindByEmailAsync(email);
            }

            return user;
        }

        private static async Task<IdentityResult> EnsureWindowsLoginMappingAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user,
            string providerKey)
        {
            var existingLogins = await userManager.GetLoginsAsync(user);
            if (existingLogins.Any(login =>
                string.Equals(login.LoginProvider, WindowsLoginProvider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(login.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase)))
            {
                return IdentityResult.Success;
            }

            return await userManager.AddLoginAsync(
                user,
                new UserLoginInfo(WindowsLoginProvider, providerKey, WindowsLoginDisplayName));
        }

        private static async Task<IdentityResult> EnsureUserRoleAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user,
            string roleName)
        {
            if (await userManager.IsInRoleAsync(user, roleName))
            {
                return IdentityResult.Success;
            }

            return await userManager.AddToRoleAsync(user, roleName);
        }

        private static void ApplyIdentityFallbacks(
            ActiveDirectoryUserProfile profile,
            ParsedWindowsIdentity identityInfo,
            string allowedDomain)
        {
            profile.SamAccountName ??= identityInfo.SamAccountName;
            profile.UserPrincipalName ??= identityInfo.UserPrincipalName;
            profile.DisplayName ??= identityInfo.SamAccountName;

            if (profile.Email is null && profile.UserPrincipalName is not null)
            {
                profile.Email = profile.UserPrincipalName;
            }

            if (profile.UserPrincipalName is null)
            {
                profile.UserPrincipalName = $"{identityInfo.SamAccountName}@{NormalizeDomain(allowedDomain)}";
            }
        }

        private static void ApplyProfileToUser(
            ApplicationUser user,
            ActiveDirectoryUserProfile profile,
            bool overwriteExisting)
        {
            user.Email = ResolveValue(user.Email, profile.Email, 256, overwriteExisting);
            user.EmailConfirmed = user.EmailConfirmed || !string.IsNullOrWhiteSpace(user.Email);
            user.FirstName = ResolveValue(user.FirstName, profile.GivenName, 100, overwriteExisting);
            user.LastName = ResolveValue(user.LastName, profile.Surname, 100, overwriteExisting);
            user.DisplayName = ResolveValue(
                user.DisplayName,
                profile.DisplayName ?? BuildNameFromParts(profile.GivenName, profile.Surname),
                150,
                overwriteExisting);
            user.JobTitle = ResolveValue(user.JobTitle, profile.JobTitle, 150, overwriteExisting);
            user.Department = ResolveValue(user.Department, profile.Department, 150, overwriteExisting);
            user.Location = ResolveValue(user.Location, profile.Location, 150, overwriteExisting);
            user.Country = ResolveValue(user.Country, profile.Country, 100, overwriteExisting);
            user.PhoneNumber = ResolveValue(user.PhoneNumber, profile.LandlineNumber, null, overwriteExisting);
        }

        private static string? ResolveValue(
            string? existingValue,
            string? candidateValue,
            int? maxLength,
            bool overwriteExisting)
        {
            var normalizedExistingValue = NormalizeOptional(existingValue);
            var normalizedCandidateValue = TrimToLength(candidateValue, maxLength);
            if (normalizedCandidateValue is null)
            {
                return normalizedExistingValue;
            }

            if (overwriteExisting || string.IsNullOrWhiteSpace(normalizedExistingValue))
            {
                return normalizedCandidateValue;
            }

            return normalizedExistingValue;
        }

        private static string BuildWindowsLoginRedirectUrl(string returnUrl)
        {
            var query = QueryString.Create("returnUrl", NormalizeReturnUrl(returnUrl));
            return $"~{WindowsLoginEntryPath}{query}";
        }

        private static string BuildWindowsCallbackUrl(
            HttpContext context,
            string returnUrl,
            string callbackPath,
            bool challengeAttempted)
        {
            IEnumerable<KeyValuePair<string, string?>> query = [
                new("returnUrl", NormalizeReturnUrl(returnUrl)),
                new(ChallengeAttemptedQueryKey, challengeAttempted ? "true" : "false")
            ];

            return UriHelper.BuildRelative(
                context.Request.PathBase,
                callbackPath,
                QueryString.Create(query));
        }

        private static string BuildLoginRedirectUrl(string returnUrl)
        {
            var query = QueryString.Create("returnUrl", NormalizeReturnUrl(returnUrl));
            return $"~/Account/Login{query}";
        }

        private static string BuildReturnRedirectUrl(string returnUrl)
        {
            var safeReturnUrl = NormalizeReturnUrl(returnUrl);
            return $"~{safeReturnUrl}";
        }

        private static string NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return "/";
            }

            if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
            {
                return "/";
            }

            if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith("\\\\", StringComparison.Ordinal))
            {
                return "/";
            }

            return returnUrl.StartsWith("/", StringComparison.Ordinal) ? returnUrl : $"/{returnUrl}";
        }

        private static string? ResolveWindowsIdentityName(ClaimsPrincipal principal)
        {
            return NormalizeOptional(
                principal.FindFirstValue(WindowsAccountNameClaimType)
                ?? principal.FindFirstValue(ClaimTypes.Upn)
                ?? principal.Identity?.Name);
        }

        private static bool TryGetIntegratedWindowsPrincipal(ClaimsPrincipal? principal, out ClaimsPrincipal windowsPrincipal)
        {
            windowsPrincipal = default!;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var authenticationType = NormalizeOptional(principal.Identity.AuthenticationType);
            var appearsToBeWindowsIdentity =
                principal.HasClaim(claim => claim.Type == WindowsAccountNameClaimType)
                || principal.HasClaim(claim => claim.Type == ClaimTypes.PrimarySid)
                || principal.HasClaim(claim => claim.Type == ClaimTypes.Sid)
                || (authenticationType?.Contains("Negotiate", StringComparison.OrdinalIgnoreCase) ?? false)
                || (authenticationType?.Contains("NTLM", StringComparison.OrdinalIgnoreCase) ?? false)
                || (authenticationType?.Contains("Kerberos", StringComparison.OrdinalIgnoreCase) ?? false)
                || (authenticationType?.Contains("Windows", StringComparison.OrdinalIgnoreCase) ?? false);

            if (!appearsToBeWindowsIdentity)
            {
                return false;
            }

            windowsPrincipal = principal;
            return true;
        }

        private static bool TryParseWindowsIdentity(
            string? identityName,
            string allowedDomain,
            out ParsedWindowsIdentity parsedIdentity)
        {
            parsedIdentity = default;
            var normalizedIdentityName = NormalizeOptional(identityName);
            if (normalizedIdentityName is null)
            {
                return false;
            }

            var normalizedAllowedDomain = NormalizeDomain(allowedDomain);
            var allowedNetBios = GetNetBiosName(normalizedAllowedDomain);

            if (normalizedIdentityName.Contains('\\', StringComparison.Ordinal))
            {
                var windowsParts = normalizedIdentityName.Split('\\', 2, StringSplitOptions.TrimEntries);
                if (windowsParts.Length != 2)
                {
                    return false;
                }

                var domainToken = NormalizeOptional(windowsParts[0]);
                var samAccountName = NormalizeOptional(windowsParts[1]);
                if (domainToken is null || samAccountName is null)
                {
                    return false;
                }

                if (!string.Equals(domainToken, allowedNetBios, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(domainToken, normalizedAllowedDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                parsedIdentity = new ParsedWindowsIdentity(
                    domainToken,
                    samAccountName,
                    $"{samAccountName}@{normalizedAllowedDomain}");
                return true;
            }

            var atIndex = normalizedIdentityName.LastIndexOf('@');
            if (atIndex <= 0 || atIndex == normalizedIdentityName.Length - 1)
            {
                return false;
            }

            var sam = normalizedIdentityName[..atIndex];
            var domain = normalizedIdentityName[(atIndex + 1)..];
            if (!string.Equals(domain, normalizedAllowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            parsedIdentity = new ParsedWindowsIdentity(domain, sam, $"{sam}@{domain}");
            return true;
        }

        private static string BuildNameFromParts(string? givenName, string? surname)
        {
            var first = NormalizeOptional(givenName);
            var last = NormalizeOptional(surname);
            return string.Join(" ", new[] { first, last }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string NormalizeDomain(string domain)
        {
            return NormalizeOptional(domain)?.ToLowerInvariant() ?? string.Empty;
        }

        private static string GetNetBiosName(string normalizedDomain)
        {
            if (string.IsNullOrWhiteSpace(normalizedDomain))
            {
                return string.Empty;
            }

            var dotIndex = normalizedDomain.IndexOf('.');
            return dotIndex < 0 ? normalizedDomain : normalizedDomain[..dotIndex];
        }

        private static string? TrimToLength(string? value, int? maxLength)
        {
            var normalized = NormalizeOptional(value);
            if (normalized is null || maxLength is null || normalized.Length <= maxLength.Value)
            {
                return normalized;
            }

            return normalized[..maxLength.Value];
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static void SetStatusMessage(HttpContext context, string message)
        {
            context.Response.Cookies.Append(
                IdentityRedirectManager.StatusCookieName,
                message,
                new CookieOptions
                {
                    SameSite = SameSiteMode.None,
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromSeconds(5),
                });
        }

        private readonly record struct ParsedWindowsIdentity(
            string DomainToken,
            string SamAccountName,
            string? UserPrincipalName);
    }
}
