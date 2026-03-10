using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public enum HomeKanbanViewType
{
    Wiki = 0,
    Onboarding = 1
}

public enum HomeKanbanCardEntityType
{
    WikiEntry = 0,
    OnboardingProfile = 1
}

public enum HomeCommentScope
{
    Wiki = 0,
    Onboarding = 1
}

public enum MentionType
{
    User = 0,
    Entry = 1,
    Unknown = 2
}

public sealed class HomeKanbanColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool CardsDraggable { get; set; } = true;
    public bool AcceptDrop { get; set; } = true;
}

public sealed class HomeKanbanCardDto
{
    public int EntryId { get; set; }
    public HomeKanbanCardEntityType EntityType { get; set; }
    public string ColumnKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CreatorDisplayName { get; set; } = "-";
    public string? OwnerDisplayName { get; set; }
    public string CategoryOrRole { get; set; } = "-";
    public string? Subtitle { get; set; }
    public string? PrimaryLinkText { get; set; }
    public string? PrimaryLinkUrl { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public int CommentCount { get; set; }
}

public sealed class HomeKanbanBoardDto
{
    public HomeKanbanViewType ViewType { get; set; }
    public List<HomeKanbanColumnDto> Columns { get; set; } = new();
    public List<HomeKanbanCardDto> Cards { get; set; } = new();
}

public sealed class MoveCardRequest
{
    public HomeKanbanViewType ViewType { get; set; }
    public HomeKanbanCardEntityType EntityType { get; set; }
    public int EntryId { get; set; }
    public string SourceColumnKey { get; set; } = string.Empty;
    public string TargetColumnKey { get; set; } = string.Empty;
    public int TargetIndex { get; set; }
}

public sealed class ReorderColumnsRequest
{
    public HomeKanbanViewType ViewType { get; set; }
    public List<string> OrderedColumnKeys { get; set; } = new();
}

public sealed class HomeEntryCommentDto
{
    public int Id { get; set; }
    public HomeCommentScope Scope { get; set; }
    public int EntryId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? AuthorId { get; set; }
    public string AuthorDisplayName { get; set; } = "-";
    public List<MentionToken> Mentions { get; set; } = new();
    public List<CommentAttachmentDto> Attachments { get; set; } = new();
}

public sealed class CreateCommentRequest
{
    public HomeCommentScope Scope { get; set; }
    public int EntryId { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<CommentMentionInputDto> Mentions { get; set; } = new();
    public List<CommentAttachmentInputDto> Attachments { get; set; } = new();
}

public sealed class CommentMentionInputDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public sealed class MentionToken
{
    public MentionType Type { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string? DisplayText { get; set; }
    public string? TargetUrl { get; set; }
}

public sealed class CommentAttachmentInputDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class CommentAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class OnboardingQuickDetailDto
{
    public int ProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? AssignedAgentDisplayName { get; set; }
    public OnboardingProfileStatus Status { get; set; }
    public int MeasureOpenCount { get; set; }
    public int MeasureTotalCount { get; set; }
    public int ChecklistOpenCount { get; set; }
    public int ChecklistTotalCount { get; set; }
    public List<string> OpenMeasureItems { get; set; } = new();
    public List<string> OpenChecklistItems { get; set; } = new();
}

public static class HomeKanbanColumnKeys
{
    public const string WikiDrafts = "wiki-drafts";
    public const string WikiLastViews = "wiki-last-views";
    public const string WikiTemplateUsage = "wiki-template-usage";
    public const string WikiFavoriteUsage = "wiki-favorite-usage";

    public const string OnboardingDrafts = "onboarding-drafts";
    public const string OnboardingNotStarted = "onboarding-not-started";
    public const string OnboardingInProgress = "onboarding-in-progress";
    public const string OnboardingCompleted = "onboarding-completed";
    public const string OnboardingArchived = "onboarding-archived";
}
