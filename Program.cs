using System.IO;
using System.Linq;
using DevExpress.AspNetCore.Reporting;
using DevExpress.Blazor.Reporting;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wiki_Blaze.Components;
using Wiki_Blaze.Components.Account;
using Wiki_Blaze.Data;
using Wiki_Blaze.Services;
using Wiki_Blaze.Services.Authentication;


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDevExpressBlazor(options =>
{
    options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
});
builder.Services.AddMvc().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddDevExpressBlazorReporting();
builder.Services.AddScoped<ReportStorageWebExtension, ReportStorage>();
builder.Services.ConfigureReportingServices(configurator =>
{
    configurator.ConfigureReportDesigner(designerConfigurator =>
    {
    });
    configurator.UseAsyncEngine();
});

builder.Services.AddDevExpressServerSideBlazorPdfViewer();

builder.Services.AddDevExpressServerSideBlazorReportViewer();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
authenticationBuilder.AddIdentityCookies();
authenticationBuilder.AddNegotiate();
builder.Services.AddAuthorization();
builder.Services.Configure<WindowsAuthenticationOptions>(
    builder.Configuration.GetSection(WindowsAuthenticationOptions.SectionName));

var dataDirectoryPath = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDirectoryPath);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

if (!string.IsNullOrWhiteSpace(connectionStringBuilder.AttachDBFilename))
{
    var attachDbFilename = connectionStringBuilder.AttachDBFilename;

    if (!Path.IsPathRooted(attachDbFilename))
    {
        attachDbFilename = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, attachDbFilename));
    }

    Directory.CreateDirectory(Path.GetDirectoryName(attachDbFilename)!);
    connectionStringBuilder.AttachDBFilename = attachDbFilename;
    connectionString = connectionStringBuilder.ConnectionString;
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

// DbContextFactory registrieren
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Wiki + Onboarding Services registrieren
builder.Services.AddScoped<IWikiService, WikiService>();
builder.Services.AddScoped<IWikiFavoriteGroupService, WikiFavoriteGroupService>();
builder.Services.AddScoped<IUserIdResolver, UserIdResolver>();
builder.Services.AddScoped<IActiveDirectoryUserProfileService, ActiveDirectoryUserProfileService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<NotificationRefreshSignal>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(serviceProvider => serviceProvider.GetRequiredService<NotificationService>());
builder.Services.AddScoped<INotificationSchedulerService>(serviceProvider => serviceProvider.GetRequiredService<NotificationService>());
builder.Services.AddHostedService<NotificationSchedulerHostedService>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var pendingMigrationsList = pendingMigrations.ToList();
        if (pendingMigrationsList.Count > 0)
        {
            startupLogger.LogWarning(
                "Pending database migrations detected ({Count}): {Migrations}. Notifications may remain inactive until 'dotnet ef database update' is applied.",
                pendingMigrationsList.Count,
                string.Join(", ", pendingMigrationsList));
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Could not determine pending migrations at startup.");
    }

    var windowsOptions = scope.ServiceProvider
        .GetRequiredService<IOptions<WindowsAuthenticationOptions>>()
        .Value;
    startupLogger.LogInformation(
        "Windows authentication config: Enabled={Enabled}, AllowedDomain={AllowedDomain}, AutoProvision={AutoProvision}, ProfileSyncMode={ProfileSyncMode}, DirectoryServicesEnabled={DirectoryServicesEnabled}, DomainController={DomainController}, Container={Container}",
        windowsOptions.Enabled,
        windowsOptions.AllowedDomain,
        windowsOptions.AutoProvision,
        windowsOptions.ProfileSyncMode,
        windowsOptions.DirectoryServices.Enabled,
        string.IsNullOrWhiteSpace(windowsOptions.DirectoryServices.DomainController) ? "<default>" : windowsOptions.DirectoryServices.DomainController,
        string.IsNullOrWhiteSpace(windowsOptions.DirectoryServices.Container) ? "<default>" : windowsOptions.DirectoryServices.Container);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var roles = new[] { "Admin", "User", "OnboardingManager" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = configuration["AdminSeed:Email"];
    var adminPassword = configuration["AdminSeed:Password"];
    var adminUserName = configuration["AdminSeed:UserName"] ?? adminEmail;
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Admin seed user creation failed: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

app.UseDevExpressBlazorReporting();
app.UseReporting(builder =>
{
    builder.UserDesignerOptions.DataBindingMode = DevExpress.XtraReports.UI.DataBindingMode.Expressions;
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
var windowsLoginRequestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WindowsLogin");
app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value;
    var isWindowsLoginRequest =
        string.Equals(requestPath, "/Account/LoginWithWindows", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestPath, "/Account/LoginWithWindowsCallback", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestPath, "/Account/LoginWithKerberos", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestPath, "/Account/LoginWithKerberosCallback", StringComparison.OrdinalIgnoreCase);

    if (!isWindowsLoginRequest)
    {
        await next();
        return;
    }

    windowsLoginRequestLogger.LogInformation(
        "Windows login request started. Path={Path}, ChallengeAttempted={ChallengeAttempted}, IsAuthenticated={IsAuthenticated}, AuthType={AuthType}",
        requestPath,
        context.Request.Query["challengeAttempted"].ToString(),
        context.User.Identity?.IsAuthenticated == true,
        context.User.Identity?.AuthenticationType ?? "<none>");

    await next();

    windowsLoginRequestLogger.LogInformation(
        "Windows login request completed. Path={Path}, StatusCode={StatusCode}",
        requestPath,
        context.Response.StatusCode);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.MapAdditionalIdentityEndpoints();

app.MapControllers();

app.Run();
