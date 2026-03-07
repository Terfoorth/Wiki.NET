using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class ReminderCandidateProvider : IReminderCandidateProvider
{
    private static readonly HashSet<int> ReminderStagesInDays = [3, 1];

    private static readonly string[] LegacyDateFormats =
    [
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy"
    ];

    public async Task<IReadOnlyList<ReminderCandidate>> GetCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (normalizedUserId is null)
        {
            return Array.Empty<ReminderCandidate>();
        }

        var candidates = new List<ReminderCandidate>();
        candidates.AddRange(await BuildWikiReviewCandidatesAsync(context, normalizedUserId, todayLocal, cancellationToken));
        candidates.AddRange(await BuildOnboardingCandidatesAsync(context, normalizedUserId, todayLocal, userTimeZone, cancellationToken));
        return candidates;
    }

    private static async Task<List<ReminderCandidate>> BuildWikiReviewCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        CancellationToken cancellationToken)
    {
        var rawValues = await context.WikiPageAttributeValues
            .AsNoTracking()
            .Where(value =>
                value.WikiPage != null
                && value.WikiPage.OwnerId == userId
                && value.AttributeDefinition != null
                && value.AttributeDefinition.Name == "ReviewDate")
            .Select(value => new
            {
                value.WikiPageId,
                PageTitle = value.WikiPage!.Title,
                value.Value
            })
            .ToListAsync(cancellationToken);

        var result = new List<ReminderCandidate>();
        foreach (var item in rawValues)
        {
            if (!TryParseLegacyDate(item.Value, out var dueDate))
            {
                continue;
            }

            var daysUntilDue = dueDate.DayNumber - todayLocal.DayNumber;
            if (!ReminderStagesInDays.Contains(daysUntilDue))
            {
                continue;
            }

            var dayLabel = daysUntilDue == 1 ? "Tag" : "Tagen";
            var dueDateDisplay = dueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            result.Add(new ReminderCandidate(
                UserNotificationType.WikiReviewDate,
                item.WikiPageId,
                daysUntilDue,
                dueDate,
                $"Review in {daysUntilDue} {dayLabel}",
                $"Der Wiki-Eintrag \"{item.PageTitle}\" erreicht sein Review-Datum am {dueDateDisplay}.",
                $"/wiki/view/{item.WikiPageId}"));
        }

        return result;
    }

    private static async Task<List<ReminderCandidate>> BuildOnboardingCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone,
        CancellationToken cancellationToken)
    {
        var profiles = await context.OnboardingProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.AssignedAgentUserId == userId
                && profile.Status != OnboardingProfileStatus.Completed
                && profile.Status != OnboardingProfileStatus.Archived)
            .Select(profile => new
            {
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.FullName,
                profile.StartDate,
                profile.TargetDate
            })
            .ToListAsync(cancellationToken);

        var result = new List<ReminderCandidate>();

        foreach (var profile in profiles)
        {
            var profileName = BuildProfileName(profile.Id, profile.FirstName, profile.LastName, profile.FullName);
            if (profile.StartDate.HasValue)
            {
                AddOnboardingCandidate(
                    result,
                    profile.Id,
                    profileName,
                    profile.StartDate.Value,
                    UserNotificationType.OnboardingStartDate,
                    "Startdatum",
                    todayLocal,
                    userTimeZone);
            }

            if (profile.TargetDate.HasValue)
            {
                AddOnboardingCandidate(
                    result,
                    profile.Id,
                    profileName,
                    profile.TargetDate.Value,
                    UserNotificationType.OnboardingTargetDate,
                    "Zieldatum",
                    todayLocal,
                    userTimeZone);
            }
        }

        return result;
    }

    private static void AddOnboardingCandidate(
        ICollection<ReminderCandidate> target,
        int profileId,
        string profileName,
        DateTime dueDateValue,
        UserNotificationType type,
        string label,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone)
    {
        var dueDateLocal = ToDateOnlyInTimeZone(dueDateValue, userTimeZone);
        var daysUntilDue = dueDateLocal.DayNumber - todayLocal.DayNumber;

        if (!ReminderStagesInDays.Contains(daysUntilDue))
        {
            return;
        }

        var dayLabel = daysUntilDue == 1 ? "Tag" : "Tagen";
        var dueDateDisplay = dueDateLocal.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        target.Add(new ReminderCandidate(
            type,
            profileId,
            daysUntilDue,
            dueDateLocal,
            $"{label} in {daysUntilDue} {dayLabel}",
            $"Beim Onboarding-Profil \"{profileName}\" liegt das {label} am {dueDateDisplay}.",
            $"/reportdesigner/details/{profileId}"));
    }

    private static DateOnly ToDateOnlyInTimeZone(DateTime value, TimeZoneInfo timeZone)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(value, timeZone));
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, timeZone));
        }

        return DateOnly.FromDateTime(value);
    }

    private static bool TryParseLegacyDate(string? value, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, LegacyDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var invariantDateTime))
        {
            date = DateOnly.FromDateTime(invariantDateTime);
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var currentDateTime))
        {
            date = DateOnly.FromDateTime(currentDateTime);
            return true;
        }

        return false;
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static string? NormalizeUserId(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
    }

    private static string BuildProfileName(int profileId, string? firstName, string? lastName, string? fullName)
    {
        var combined = string.Join(" ", new[] { firstName?.Trim(), lastName?.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        return string.IsNullOrWhiteSpace(fullName) ? $"Profil #{profileId}" : fullName;
    }
}
