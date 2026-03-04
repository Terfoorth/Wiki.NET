using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public interface IOnboardingService
{
    Task<List<OnboardingProfileListItem>> GetProfilesAsync(
        string? searchText = null,
        OnboardingProfileStatus? status = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default);

    Task<OnboardingProfile?> GetProfileAsync(int profileId, CancellationToken cancellationToken = default);

    Task<OnboardingProfile> CreateProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default);

    Task<OnboardingProfile> UpdateProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default);

    Task DeleteProfileAsync(int profileId, CancellationToken cancellationToken = default);

    Task<OnboardingProfile?> DuplicateProfileAsync(int sourceProfileId, CancellationToken cancellationToken = default);

    Task<List<OnboardingMeasureCatalogItem>> GetMeasureCatalogAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<OnboardingMeasureCatalogItem> SaveMeasureCatalogItemAsync(OnboardingMeasureCatalogItem item, CancellationToken cancellationToken = default);

    Task SetMeasureCatalogItemActiveAsync(int catalogItemId, bool isActive, CancellationToken cancellationToken = default);

    Task<List<OnboardingChecklistCatalogItem>> GetChecklistCatalogAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<OnboardingChecklistCatalogItem> SaveChecklistCatalogItemAsync(OnboardingChecklistCatalogItem item, CancellationToken cancellationToken = default);

    Task SetChecklistCatalogItemActiveAsync(int catalogItemId, bool isActive, CancellationToken cancellationToken = default);

    Task<OnboardingMeasureEntry> AddMeasureEntryAsync(int profileId, int catalogItemId, string value, string? notes, CancellationToken cancellationToken = default);

    Task<OnboardingMeasureEntry?> UpdateMeasureEntryAsync(OnboardingMeasureEntry entry, CancellationToken cancellationToken = default);

    Task<bool> DeleteMeasureEntryAsync(int measureEntryId, CancellationToken cancellationToken = default);

    Task<bool> SetMeasureEntryCompletedAsync(int measureEntryId, bool isCompleted, CancellationToken cancellationToken = default);

    Task<OnboardingChecklistEntry?> UpdateChecklistEntryAsync(OnboardingChecklistEntry entry, CancellationToken cancellationToken = default);

    Task<bool> SetChecklistEntryCompletedAsync(int checklistEntryId, bool isCompleted, CancellationToken cancellationToken = default);

    Task<bool> SetChecklistEntryResultAsync(int checklistEntryId, OnboardingChecklistResult result, CancellationToken cancellationToken = default);
}

public sealed class OnboardingProfileListItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Supervisor { get; set; }
    public string? Email { get; set; }
    public OnboardingProfileStatus Status { get; set; }
    public int MeasureCompleted { get; set; }
    public int MeasureTotal { get; set; }
    public int ChecklistCompleted { get; set; }
    public int ChecklistTotal { get; set; }
    public int ProgressPercent { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public int OpenTodoCount => (MeasureTotal - MeasureCompleted) + (ChecklistTotal - ChecklistCompleted);
}
