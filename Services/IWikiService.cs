using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services
{
    /// <summary>
    /// Definition der Wiki-Service-Schnittstelle.
    /// </summary>
    public interface IWikiService
    {
        // --- Kategorien ---
        Task<List<WikiCategory>> GetCategoriesAsync();
        Task<List<WikiCategory>> GetFormCategoriesAsync();
        Task SaveCategoryAsync(WikiCategory category);
        Task DeleteCategoryAsync(WikiCategory category);

        // --- Wiki Seiten ---
        Task<List<WikiPage>> GetPagesAsync();
        Task<List<WikiPage>> GetWikiFormsAsync(string? userId);
        Task<List<WikiPage>> GetDashboardPagesAsync(string? userId);
        Task<WikiPage?> GetPageByIdAsync(int id);
        Task<WikiPage?> GetPageForUserAsync(int pageId, string? userId);
        Task<WikiPage?> GetWikiFormForUserAsync(int pageId, string? userId);
        Task<List<WikiPage>> GetPagesByFilterAsync(WikiPageStatus? status, string? ownerId, string? authorId, WikiPageVisibility? visibility);
        Task<List<WikiPage>> GetDraftsAsync(string userId);
        Task<List<WikiPage>> GetTemplatesAsync();
        Task<WikiPage?> CreateEntryFromTemplateAsync(string userId, int templatePageId, WikiPageStatus targetStatus);
        Task SavePageAsync(WikiPage page);
        Task DeletePageAsync(int id);

        // --- Favoriten ---
        Task AddOrMoveFavoriteAsync(string userId, int pageId, int? groupId = null);
        Task AssignFavoriteToGroupAsync(string userId, int pageId, int groupId);
        Task AddFavoriteAsync(string userId, int pageId, int? groupId = null);
        Task RemoveFavoriteAsync(string userId, int pageId);
        Task<List<WikiPageFavorite>> GetFavoritesAsync(string userId);
        Task<List<WikiPageFavorite>> GetFavoritesByGroupAsync(string userId, int groupId);
        Task<Dictionary<int, List<WikiPageFavorite>>> GetFavoritesGroupedAsync(string userId);
        Task<List<WikiFavoriteGroup>> GetFavoriteGroupsAsync(string userId);
        Task<WikiFavoriteGroup> CreateFavoriteGroupAsync(string userId, string name);
        Task<WikiFavoriteGroup> RenameFavoriteGroupAsync(string userId, int groupId, string name);
        Task DeleteFavoriteGroupAsync(string userId, int groupId, bool preventDeletionIfNotEmpty = false);
        Task<List<WikiTemplateGroup>> GetTemplateGroupsAsync(string userId);
        Task<WikiTemplateGroup> CreateTemplateGroupAsync(string userId, string name);
        Task<WikiTemplateGroup> RenameTemplateGroupAsync(string userId, int groupId, string name);
        Task DeleteTemplateGroupAsync(string userId, int groupId, bool preventDeletionIfNotEmpty = false);
        Task AssignTemplateToGroupAsync(string userId, int pageId, int groupId);

        // --- Attribute ---
        Task<List<WikiAttributeDefinition>> GetAttributeDefinitionsAsync();
        Task<WikiAttributeDefinition?> GetAttributeDefinitionByIdAsync(int id);
        Task SaveAttributeDefinitionAsync(WikiAttributeDefinition definition);
        Task DeleteAttributeDefinitionAsync(int id);

        Task<List<WikiPageAttributeValue>> GetAttributeValuesAsync(int pageId);
        Task SaveAttributeValueAsync(WikiPageAttributeValue value);
        Task DeleteAttributeValueAsync(int id);

        // --- Kommentare (legacy: ersetzt durch Home-Kommentar-Modul) ---
        Task<List<WikiComment>> GetCommentsAsync(int pageId);
        Task<WikiComment?> GetCommentByIdAsync(int id);
        Task SaveCommentAsync(WikiComment comment);
        Task DeleteCommentAsync(int id);

        // --- Home Kanban --- 
        Task<HomeKanbanBoardDto> GetHomeKanbanBoardAsync(string userId, int takePerColumn = 25);
        Task<bool> MoveHomeKanbanCardAsync(string userId, MoveCardRequest request);
        Task SaveHomeKanbanColumnOrderAsync(string userId, ReorderColumnsRequest request);

        // --- Home Kommentare ---
        Task<List<HomeEntryCommentDto>> GetHomeCommentsAsync(HomeCommentScope scope, int entryId, string? userId);
        Task<HomeEntryCommentDto> AddHomeCommentAsync(string? userId, CreateCommentRequest request);
        Task DeleteHomeCommentAsync(string? userId, int commentId);

        // --- Home Tracking ---
        Task RecordWikiEntryViewAsync(string? userId, int pageId);
        Task RecordTemplateUsageAsync(string? userId, int pageId);
        Task RecordFavoriteUsageAsync(string? userId, int pageId);

        // --- Zuweisungen ---
        Task<List<WikiAssignment>> GetAssignmentsAsync(int pageId);
        Task<List<WikiAssignment>> GetAssignmentsByAssigneeAsync(string assigneeId, WikiAssignmentStatus? status);
        Task SaveAssignmentAsync(WikiAssignment assignment);
        Task DeleteAssignmentAsync(int id);

        // --- Änderungsverlauf ---
        Task<List<WikiChangeLog>> GetChangeLogsAsync(int pageId);
        Task<List<WikiChangeLog>> GetChangeLogsByAuthorAsync(string authorId);
        Task SaveChangeLogEntryAsync(WikiChangeLog changeLog);
        Task DeleteChangeLogEntryAsync(int id);
    }
}
