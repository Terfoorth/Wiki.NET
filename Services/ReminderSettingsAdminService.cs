using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class ReminderSettingsAdminService(
    IHostEnvironment hostEnvironment,
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<ReminderSettingsAdminService> logger) : IReminderSettingsAdminService
{
    private static readonly JsonSerializerOptions JsonWriterOptions = new() { WriteIndented = true };

    public async Task<ReminderAdminSettingsView> GetAsync(string adminUserId, CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var (root, _) = await LoadSettingsRootAsync(cancellationToken);
        var reminderSection = EnsureObject(root, ReminderEmailOptions.SectionName);
        var smtpSection = EnsureObject(root, ReminderSmtpOptions.SectionName);

        var view = new ReminderAdminSettingsView
        {
            Enabled = ReadBool(reminderSection, nameof(ReminderEmailOptions.Enabled), false),
            SendHourLocal = ReadInt(reminderSection, nameof(ReminderEmailOptions.SendHourLocal), 8),
            BaseUrl = ReadString(reminderSection, nameof(ReminderEmailOptions.BaseUrl), string.Empty),
            Host = ReadString(smtpSection, nameof(ReminderSmtpOptions.Host), string.Empty),
            Port = ReadInt(smtpSection, nameof(ReminderSmtpOptions.Port), 587),
            UseSsl = ReadBool(smtpSection, nameof(ReminderSmtpOptions.UseSsl), true),
            Username = ReadString(smtpSection, nameof(ReminderSmtpOptions.Username), string.Empty),
            FromAddress = ReadString(smtpSection, nameof(ReminderSmtpOptions.FromAddress), string.Empty),
            FromName = ReadString(smtpSection, nameof(ReminderSmtpOptions.FromName), string.Empty)
        };

        view.PasswordConfigured = !string.IsNullOrWhiteSpace(ReadString(smtpSection, nameof(ReminderSmtpOptions.Password), string.Empty));

        return view;
    }

    public async Task SaveAsync(
        string adminUserId,
        ReminderAdminSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);
        ValidateUpdate(update);

        var (root, settingsPath) = await LoadSettingsRootAsync(cancellationToken);
        var reminderSection = EnsureObject(root, ReminderEmailOptions.SectionName);
        var smtpSection = EnsureObject(root, ReminderSmtpOptions.SectionName);

        var passwordUpdated = !string.IsNullOrWhiteSpace(update.NewPassword);

        reminderSection[nameof(ReminderEmailOptions.Enabled)] = update.Enabled;
        reminderSection[nameof(ReminderEmailOptions.SendHourLocal)] = update.SendHourLocal;
        reminderSection[nameof(ReminderEmailOptions.BaseUrl)] = update.BaseUrl.Trim();

        smtpSection[nameof(ReminderSmtpOptions.Host)] = update.Host.Trim();
        smtpSection[nameof(ReminderSmtpOptions.Port)] = update.Port;
        smtpSection[nameof(ReminderSmtpOptions.UseSsl)] = update.UseSsl;
        smtpSection[nameof(ReminderSmtpOptions.Username)] = NormalizeOptional(update.Username);
        smtpSection[nameof(ReminderSmtpOptions.FromAddress)] = update.FromAddress.Trim();
        smtpSection[nameof(ReminderSmtpOptions.FromName)] = NormalizeOptional(update.FromName);

        if (passwordUpdated)
        {
            smtpSection[nameof(ReminderSmtpOptions.Password)] = update.NewPassword;
        }

        await File.WriteAllTextAsync(settingsPath, root.ToJsonString(JsonWriterOptions), cancellationToken);
        await WriteAuditLogAsync(adminUserId, update, passwordUpdated, cancellationToken);

        logger.LogInformation("Reminder settings were updated by admin user {AdminUserId}.", adminUserId);
    }

    private async Task EnsureAdminAsync(string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new UnauthorizedAccessException("Admin user id is required.");
        }

        var user = await userManager.FindByIdAsync(adminUserId);
        if (user is null || !await userManager.IsInRoleAsync(user, "Admin"))
        {
            throw new UnauthorizedAccessException("Only admin users can access reminder settings.");
        }
    }

    private async Task<(JsonObject Root, string SettingsPath)> LoadSettingsRootAsync(CancellationToken cancellationToken)
    {
        var settingsPath = Path.Combine(hostEnvironment.ContentRootPath, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException($"Configuration file was not found: {settingsPath}");
        }

        await using var stream = File.OpenRead(settingsPath);
        var parsedNode = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = parsedNode as JsonObject ?? new JsonObject();
        return (root, settingsPath);
    }

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static void ValidateUpdate(ReminderAdminSettingsUpdate update)
    {
        if (update.SendHourLocal is < 0 or > 23)
        {
            throw new InvalidOperationException("Send hour must be between 0 and 23.");
        }

        if (!Uri.TryCreate(update.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Base URL must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(update.Host))
        {
            throw new InvalidOperationException("SMTP host is required.");
        }

        if (update.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("SMTP port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(update.FromAddress))
        {
            throw new InvalidOperationException("SMTP from address is required.");
        }

        _ = new MailAddress(update.FromAddress);
    }

    private async Task WriteAuditLogAsync(
        string adminUserId,
        ReminderAdminSettingsUpdate update,
        bool passwordUpdated,
        CancellationToken cancellationToken)
    {
        var details =
            $"Enabled={update.Enabled};" +
            $"SendHourLocal={update.SendHourLocal};" +
            $"BaseUrl={update.BaseUrl.Trim()};" +
            $"Host={update.Host.Trim()};" +
            $"Port={update.Port};" +
            $"UseSsl={update.UseSsl};" +
            $"FromAddress={update.FromAddress.Trim()};" +
            $"FromName={NormalizeOptional(update.FromName) ?? string.Empty};" +
            $"Username={NormalizeOptional(update.Username) ?? string.Empty};" +
            $"PasswordUpdated={passwordUpdated}";

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);
        context.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = "ReminderSettingsUpdated",
            Subject = "ReminderEmailAndSmtp",
            Details = details,
            CreatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string ReadString(JsonObject source, string key, string fallback)
    {
        return source[key]?.GetValue<string>() ?? fallback;
    }

    private static int ReadInt(JsonObject source, string key, int fallback)
    {
        return source[key]?.GetValue<int>() ?? fallback;
    }

    private static bool ReadBool(JsonObject source, string key, bool fallback)
    {
        return source[key]?.GetValue<bool>() ?? fallback;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
