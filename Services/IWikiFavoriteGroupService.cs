using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services
{
    public interface IWikiFavoriteGroupService
    {
        Task<WikiFavoriteGroup?> EnsureDefaultGroupAsync(string userId);
    }
}
