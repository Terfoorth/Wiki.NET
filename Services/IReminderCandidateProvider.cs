using Wiki_Blaze.Data;

namespace Wiki_Blaze.Services;

public interface IReminderCandidateProvider
{
    Task<IReadOnlyList<ReminderCandidate>> GetCandidatesAsync(
        ApplicationDbContext context,
        string userId,
        DateOnly todayLocal,
        TimeZoneInfo userTimeZone,
        CancellationToken cancellationToken = default);
}
