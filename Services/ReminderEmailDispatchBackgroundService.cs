using System.Text.Encodings.Web;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class ReminderEmailDispatchBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ReminderEmailOptions> reminderOptionsMonitor,
    ILogger<ReminderEmailDispatchBackgroundService> logger) : BackgroundService
{
    private static readonly HtmlEncoder HtmlEncoder = HtmlEncoder.Default;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunDispatchCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunDispatchCycleAsync(stoppingToken);
        }
    }

    private async Task RunDispatchCycleAsync(CancellationToken cancellationToken)
    {
        var reminderOptions = reminderOptionsMonitor.CurrentValue;
        if (!reminderOptions.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(reminderOptions.BaseUrl, UriKind.Absolute, out _))
        {
            logger.LogWarning("Reminder email dispatch skipped because ReminderEmail:BaseUrl is invalid.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var candidateProvider = scope.ServiceProvider.GetRequiredService<IReminderCandidateProvider>();
        var reminderEmailSender = scope.ServiceProvider.GetRequiredService<IReminderEmailSender>();

        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var recipients = await context.Users
            .AsNoTracking()
            .Where(user =>
                user.ReceiveReminderEmails
                && user.EmailConfirmed
                && user.Email != null
                && user.Email != string.Empty)
            .Select(user => new ReminderRecipient(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.FirstName,
                user.LastName,
                user.TimeZone))
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userTimeZone = ReminderCandidateProvider.ResolveTimeZone(recipient.TimeZoneId);
            var userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);
            if (userNow.Hour != reminderOptions.SendHourLocal)
            {
                continue;
            }

            var todayLocal = DateOnly.FromDateTime(userNow);
            var candidates = await candidateProvider.GetCandidatesAsync(context, recipient.UserId, todayLocal, userTimeZone, cancellationToken);
            if (candidates.Count == 0)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessCandidateAsync(context, reminderEmailSender, reminderOptions, recipient, candidate, cancellationToken);
            }

            context.ChangeTracker.Clear();
        }
    }

    private async Task ProcessCandidateAsync(
        ApplicationDbContext context,
        IReminderEmailSender reminderEmailSender,
        ReminderEmailOptions reminderOptions,
        ReminderRecipient recipient,
        ReminderCandidate candidate,
        CancellationToken cancellationToken)
    {
        var dueDateUtc = ToUtcDate(candidate.DueDate);
        var nowUtc = DateTime.UtcNow;

        var dispatch = await context.ReminderEmailDispatches
            .FirstOrDefaultAsync(item =>
                item.UserId == recipient.UserId
                && item.Type == candidate.Type
                && item.SourceEntityId == candidate.SourceEntityId
                && item.StageDaysBefore == candidate.StageDaysBefore
                && item.DueDate == dueDateUtc,
                cancellationToken);

        if (dispatch is not null && dispatch.SentAtUtc.HasValue)
        {
            return;
        }

        if (dispatch is null)
        {
            dispatch = new ReminderEmailDispatch
            {
                UserId = recipient.UserId,
                Type = candidate.Type,
                SourceEntityId = candidate.SourceEntityId,
                StageDaysBefore = candidate.StageDaysBefore,
                DueDate = dueDateUtc,
                CreatedAtUtc = nowUtc
            };

            context.ReminderEmailDispatches.Add(dispatch);
        }

        dispatch.AttemptCount += 1;
        dispatch.LastAttemptAtUtc = nowUtc;

        try
        {
            var message = BuildMessage(reminderOptions.BaseUrl, recipient, candidate);
            await reminderEmailSender.SendAsync(message, cancellationToken);

            dispatch.SentAtUtc = DateTime.UtcNow;
            dispatch.LastError = null;
        }
        catch (Exception ex)
        {
            dispatch.LastError = Truncate(ex.Message, 2000);
            logger.LogError(
                ex,
                "Failed to send reminder email for user {UserId}, type {Type}, source {SourceId}, stage {StageDaysBefore}.",
                recipient.UserId,
                candidate.Type,
                candidate.SourceEntityId,
                candidate.StageDaysBefore);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            logger.LogDebug(
                "Duplicate reminder dispatch entry detected for user {UserId}, type {Type}, source {SourceId}, stage {StageDaysBefore}.",
                recipient.UserId,
                candidate.Type,
                candidate.SourceEntityId,
                candidate.StageDaysBefore);
        }
    }

    private static ReminderEmailMessage BuildMessage(string baseUrl, ReminderRecipient recipient, ReminderCandidate candidate)
    {
        var subject = $"Erinnerung: {candidate.Title}";
        var absoluteUrl = BuildAbsoluteUrl(baseUrl, candidate.TargetUrl);

        var displayName = ResolveDisplayName(recipient);
        var encodedDisplayName = HtmlEncoder.Encode(displayName);
        var encodedMessage = HtmlEncoder.Encode(candidate.Message);
        var encodedUrl = HtmlEncoder.Encode(absoluteUrl);

        var htmlBody =
            $"<p>Hallo {encodedDisplayName},</p>" +
            $"<p>{encodedMessage}</p>" +
            $"<p><a href=\"{encodedUrl}\">Zur Aufgabe</a></p>";

        var textBody =
            $"Hallo {displayName},{Environment.NewLine}{Environment.NewLine}" +
            $"{candidate.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Zur Aufgabe: {absoluteUrl}";

        return new ReminderEmailMessage(
            recipient.Email,
            subject,
            htmlBody,
            textBody);
    }

    private static string BuildAbsoluteUrl(string baseUrl, string targetUrl)
    {
        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return targetUrl;
        }

        return new Uri(baseUri, targetUrl).ToString();
    }

    private static string ResolveDisplayName(ReminderRecipient recipient)
    {
        if (!string.IsNullOrWhiteSpace(recipient.DisplayName))
        {
            return recipient.DisplayName;
        }

        var combined = string.Join(" ", new[] { recipient.FirstName?.Trim(), recipient.LastName?.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        return recipient.Email;
    }

    private static DateTime ToUtcDate(DateOnly date)
    {
        return DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return false;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }

    private sealed record ReminderRecipient(
        string UserId,
        string Email,
        string? DisplayName,
        string? FirstName,
        string? LastName,
        string? TimeZoneId);
}
