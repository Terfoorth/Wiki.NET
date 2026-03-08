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

    Task<List<OnboardingAssigneeLookupItem>> GetAssigneeLookupAsync(CancellationToken cancellationToken = default);

    Task<OnboardingProfileAttachmentInfo?> GetAttachmentInfoAsync(int profileId, CancellationToken cancellationToken = default);

    Task<OnboardingProfileAttachmentData?> GetAttachmentContentAsync(int profileId, CancellationToken cancellationToken = default);

    Task<OnboardingProfileAttachmentInfo> UploadOrReplaceAttachmentAsync(
        int profileId,
        string originalFileName,
        string contentType,
        byte[] content,
        string? uploadedByUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAttachmentAsync(int profileId, CancellationToken cancellationToken = default);

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

    Task<HomeKanbanBoardDto> GetHomeKanbanBoardAsync(string userId, int takePerColumn = 25, CancellationToken cancellationToken = default);

    Task<bool> MoveHomeKanbanCardAsync(string userId, MoveCardRequest request, CancellationToken cancellationToken = default);

    Task SaveHomeKanbanColumnOrderAsync(string userId, ReorderColumnsRequest request, CancellationToken cancellationToken = default);

    Task<OnboardingQuickDetailDto?> GetQuickDetailAsync(int profileId, CancellationToken cancellationToken = default);

    Task<List<HomeEntryCommentDto>> GetHomeCommentsAsync(int profileId, string? userId, CancellationToken cancellationToken = default);

    Task<HomeEntryCommentDto> AddHomeCommentAsync(string? userId, CreateCommentRequest request, CancellationToken cancellationToken = default);

    Task DeleteHomeCommentAsync(string? userId, int commentId, CancellationToken cancellationToken = default);
}

public sealed class OnboardingProfileListItem
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? Supervisor { get; set; }
    public string? Email { get; set; }
    public DateTime EntryDate { get; set; }
    public string? AssignedAgentDisplayName { get; set; }
    public OnboardingProfileStatus Status { get; set; }
    public int MeasureCompleted { get; set; }
    public int MeasureTotal { get; set; }
    public int ChecklistCompleted { get; set; }
    public int ChecklistTotal { get; set; }
    public int ProgressPercent { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public int OpenTodoCount => (MeasureTotal - MeasureCompleted) + (ChecklistTotal - ChecklistCompleted);
}

public sealed class OnboardingAssigneeLookupItem
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
}

public sealed class OnboardingProfileAttachmentInfo
{
    public int ProfileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedByUserId { get; set; }
    public string? UploadedByDisplayName { get; set; }
}

public sealed class OnboardingProfileAttachmentData
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
