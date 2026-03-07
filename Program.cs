using System.IO;
using System.Linq;
using DevExpress.AspNetCore.Reporting;
using DevExpress.Blazor.Reporting;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Components;
using Wiki_Blaze.Components.Account;
using Wiki_Blaze.Data;
using Wiki_Blaze.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDevExpressBlazor(options =>
{
    options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
});
builder.Services.AddMvc();

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
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

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
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReminderCandidateProvider, ReminderCandidateProvider>();
builder.Services.AddSingleton<IReminderEmailSender, SmtpReminderEmailSender>();
builder.Services.AddScoped<IReminderSettingsAdminService, ReminderSettingsAdminService>();
builder.Services.AddHostedService<ReminderEmailDispatchBackgroundService>();

builder.Services.AddOptions<ReminderEmailOptions>()
    .Bind(builder.Configuration.GetSection(ReminderEmailOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ReminderSmtpOptions>()
    .Bind(builder.Configuration.GetSection(ReminderSmtpOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
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
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.MapAdditionalIdentityEndpoints();

app.Run();
