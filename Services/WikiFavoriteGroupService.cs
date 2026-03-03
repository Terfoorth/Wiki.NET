using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services
{
    public class WikiFavoriteGroupService : IWikiFavoriteGroupService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public WikiFavoriteGroupService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<WikiFavoriteGroup?> EnsureDefaultGroupAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            await using var context = await _dbFactory.CreateDbContextAsync();

            var existingGroup = await context.WikiFavoriteGroups
                .FirstOrDefaultAsync(group => group.UserId == userId && group.Name == WikiFavoriteGroup.DefaultGroupName);

            if (existingGroup is not null)
            {
                return existingGroup;
            }

            var newGroup = new WikiFavoriteGroup
            {
                UserId = userId,
                Name = WikiFavoriteGroup.DefaultGroupName,
                CreatedAt = DateTime.UtcNow
            };

            context.WikiFavoriteGroups.Add(newGroup);

            try
            {
                await context.SaveChangesAsync();
                return newGroup;
            }
            catch (DbUpdateException)
            {
                return await context.WikiFavoriteGroups
                    .FirstOrDefaultAsync(group => group.UserId == userId && group.Name == WikiFavoriteGroup.DefaultGroupName);
            }
        }
    }
}
