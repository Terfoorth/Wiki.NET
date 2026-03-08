using DevExpress.Data.Helpers;
using DevExpress.Internal;
using DevExpress.XtraReports.Templates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services
{
    /// <summary>
    /// Interface für den Zugriff auf Wiki-Daten (Kategorien und Seiten).
    /// Dient der Entkopplung von UI und Datenbank.
    /// </summary>
   

    /// <summary>
    /// Implementierung des Wiki-Services mittels Entity Framework Core.
    /// Nutzt DbContextFactory für Thread-Sicherheit in Blazor Server.
    /// </summary>
    public partial class WikiService : IWikiService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public WikiService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        #region Kategorien Logik

        public async Task<List<WikiCategory>> GetCategoriesAsync()
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            // AsNoTracking verbessert die Performance für reine Lesezugriffe
            return await context.WikiCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<WikiCategory>> GetFormCategoriesAsync()
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            return await context.WikiCategories
                .AsNoTracking()
                .Where(category => category.IsFormCategory)
                .OrderBy(category => category.Name)
                .ToListAsync();
        }


        public async Task SaveCategoryAsync(WikiCategory category)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (category.Id == 0)
            {
                context.WikiCategories.Add(category);
            }
            else
            {
                context.WikiCategories.Update(category);
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(WikiCategory category)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // Optional: Prüfen, ob Abhängigkeiten bestehen, bevor blind gelöscht wird
            bool hasDependencies = await context.WikiPages.AnyAsync(p => p.CategoryId == category.Id) ||
                                   await context.WikiCategories.AnyAsync(c => c.ParentId == category.Id);

            if (hasDependencies)
            {
                throw new InvalidOperationException("Diese Kategorie kann nicht gelöscht werden, da sie noch Artikel oder Unterkategorien enthält.");
            }

            // Um ein Objekt zu löschen, muss es dem Context bekannt sein. 
            // Da 'category' hier meist ein UI-Model ist, attachen wir es oder setzen den State direkt.
            context.Entry(category).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        #endregion

        #region WikiPage Logik
        private static IQueryable<WikiPage> BuildPageQuery(ApplicationDbContext context)
        {
            return context.WikiPages
                .Include(p => p.Category)
                .Include(p => p.TemplateGroup)
                .Include(p => p.AttributeValues)
                .ThenInclude(value => value.AttributeDefinition)
                .AsNoTracking();
        }

        private static IQueryable<WikiPage> ApplyVisibilityFilter(IQueryable<WikiPage> query, ApplicationDbContext context, string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return query.Where(page => page.Visibility == WikiPageVisibility.Public);
            }

            return query.Where(page =>
                page.Visibility == WikiPageVisibility.Public
                || (page.Visibility == WikiPageVisibility.Private
                    && page.OwnerId == userId)
                || (page.Visibility == WikiPageVisibility.Team
                    && (
                        page.OwnerId == userId
                        || context.WikiAssignments.Any(assignment => assignment.WikiPageId == page.Id && assignment.AssigneeId == userId)
                    )));
        }

        private static string ResolveDisplayName(ApplicationUser? user)
        {
            if (user is null)
            {
                return "-";
            }

            if (!string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return user.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                return user.UserName;
            }

            return user.Email ?? "-";
        }

        private static async Task MapWikiPageUserDisplayNamesAsync(ApplicationDbContext context, List<WikiPage> pages)
        {
            if (pages.Count == 0)
            {
                return;
            }

            var userIds = pages
                .SelectMany(page => new[] { page.AuthorId, page.OwnerId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (userIds.Count == 0)
            {
                foreach (var page in pages)
                {
                    page.AuthorDisplayName = "-";
                    page.OwnerDisplayName = "-";
                }

                return;
            }

            var users = await context.Users
                .Where(user => userIds.Contains(user.Id))
                .AsNoTracking()
                .ToDictionaryAsync(user => user.Id);

            foreach (var page in pages)
            {
                users.TryGetValue(page.AuthorId ?? string.Empty, out var author);
                users.TryGetValue(page.OwnerId ?? string.Empty, out var owner);

                page.AuthorDisplayName = ResolveDisplayName(author);
                page.OwnerDisplayName = ResolveDisplayName(owner);
            }
        }

        private static async Task MapWikiPageUserDisplayNamesAsync(ApplicationDbContext context, WikiPage? page)
        {
            if (page is null)
            {
                return;
            }

            await MapWikiPageUserDisplayNamesAsync(context, new List<WikiPage> { page });
        }

        public async Task<List<WikiPage>> GetPagesAsync()
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // Lädt alle Seiten inkl. der verknüpften Kategorie und Attribute für die Anzeige
            var pages = await BuildPageQuery(context)
                                .Where(page => page.Status != WikiPageStatus.Template)
                .Where(page => page.EntryType == WikiEntryType.Standard)
                .OrderByDescending(p => p.LastModified) // Neueste Änderungen zuerst
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, pages);
            return pages;
        }


        public async Task<List<WikiPage>> GetWikiFormsAsync(string? userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var query = ApplyVisibilityFilter(BuildPageQuery(context), context, userId)
                .Where(page => page.EntryType == WikiEntryType.Form)
                .Where(page => page.Category != null && page.Category.IsFormCategory);

            var forms = await query
                .OrderByDescending(page => page.LastModified)
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, forms);
            return forms;
        }

        public async Task<List<WikiPage>> GetDashboardPagesAsync(string? userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var pages = await ApplyVisibilityFilter(BuildPageQuery(context), context, userId)
                .Where(page => page.Status == WikiPageStatus.Published)
                // Templates (Status=Template) are intentionally excluded from dashboard results by design.
                .Where(page => page.EntryType == WikiEntryType.Standard)
                .OrderByDescending(page => page.LastModified)
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, pages);
            return pages;
        }

        public async Task<WikiPage?> GetPageByIdAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var page = await BuildPageQuery(context)
                .FirstOrDefaultAsync(p => p.Id == id);

            await MapWikiPageUserDisplayNamesAsync(context, page);
            return page;
        }

        public async Task<WikiPage?> GetPageForUserAsync(int pageId, string? userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var page = await ApplyVisibilityFilter(BuildPageQuery(context), context, userId)
                .FirstOrDefaultAsync(p => p.Id == pageId);

            if (page is null)
            {
                return null;
            }

            await MapWikiPageUserDisplayNamesAsync(context, page);
            return page;
        }


        public async Task<WikiPage?> GetWikiFormForUserAsync(int pageId, string? userId)
        {
            var page = await GetPageForUserAsync(pageId, userId);
            return page?.EntryType == WikiEntryType.Form ? page : null;
        }

        public async Task<List<WikiPage>> GetPagesByFilterAsync(WikiPageStatus? status, string? ownerId, string? authorId, WikiPageVisibility? visibility)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var query = BuildPageQuery(context)
                .Where(page => page.EntryType == WikiEntryType.Standard);

            if (status.HasValue)
            {
                query = query.Where(page => page.Status == status.Value && page.EntryType == WikiEntryType.Standard);
            }

            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                query = query.Where(page => page.OwnerId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(authorId))
            {
                query = query.Where(page => page.AuthorId == authorId);
            }

            if (visibility.HasValue)
            {
                query = query.Where(page => page.Visibility == visibility.Value);
            }

            var pages = await query
                .OrderByDescending(page => page.LastModified)
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, pages);
            return pages;
        }

        public async Task<List<WikiPage>> GetDraftsAsync(string userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var pages = await BuildPageQuery(context)
                .Where(page =>
                    page.OwnerId == userId
                    && page.EntryType == WikiEntryType.Standard
                    && (page.Status == WikiPageStatus.Draft || page.Visibility == WikiPageVisibility.Private))
                .OrderByDescending(page => page.LastModified)
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, pages);
            return pages;
        }

        public async Task<List<WikiPage>> GetTemplatesAsync()
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var pages = await BuildPageQuery(context)
                                .Where(page => page.Status == WikiPageStatus.Template)
                .Where(page => page.EntryType == WikiEntryType.Standard)
                .OrderByDescending(page => page.LastModified)
                .ToListAsync();

            await MapWikiPageUserDisplayNamesAsync(context, pages);
            return pages;
        }

        public async Task<WikiPage?> CreateEntryFromTemplateAsync(string userId, int templatePageId, WikiPageStatus targetStatus)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var template = await context.WikiPages
                .AsNoTracking()
                .FirstOrDefaultAsync(page =>
                    page.Id == templatePageId
                    && page.Status == WikiPageStatus.Template
                    && page.EntryType == WikiEntryType.Standard);

            if (template is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var clonedPage = new WikiPage
            {
                Id = 0,
                Title = template.Title,
                Content = template.Content?.ToArray(),
                PreviewText = template.PreviewText,
                CategoryId = template.CategoryId,
                EntryType = template.EntryType,
                IsEditLocked = template.IsEditLocked,
                FormSchemaJson = template.FormSchemaJson,
                CreatedAt = now,
                LastModified = now,
                OwnerId = userId,
                AuthorId = userId,
                TemplateGroupId = null
            };

            switch (targetStatus)
            {
                case WikiPageStatus.Published:
                    clonedPage.Status = WikiPageStatus.Published;
                    clonedPage.Visibility = template.Visibility < WikiPageVisibility.Team
                        ? WikiPageVisibility.Team
                        : template.Visibility;
                    break;
                case WikiPageStatus.Draft:
                    clonedPage.Status = WikiPageStatus.Draft;
                    clonedPage.Visibility = WikiPageVisibility.Private;
                    break;
                default:
                    throw new InvalidOperationException("Aus einer Vorlage können nur Entwürfe oder veröffentlichte Einträge erstellt werden.");
            }

            await SavePageAsync(clonedPage);
            await RecordTemplateUsageAsync(userId, templatePageId);
            return await GetPageByIdAsync(clonedPage.Id);
        }

        public async Task SavePageAsync(WikiPage page)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // Zeitstempel für letzte Änderung immer aktualisieren
            page.LastModified = DateTime.UtcNow;

            if (page.Id == 0)
            {
                // Neuer Eintrag
                if (string.IsNullOrWhiteSpace(page.OwnerId))
                {
                    throw new InvalidOperationException("OwnerId muss für neue Wiki-Seiten gesetzt sein.");
                }

                // AuthorId beschreibt den ursprünglichen Ersteller.
                // Falls nicht explizit gesetzt, übernehmen wir den Owner für konsistente Bestandsdaten.
                if (string.IsNullOrWhiteSpace(page.AuthorId))
                {
                    page.AuthorId = page.OwnerId;
                }

                if (page.Status == WikiPageStatus.Template)
                {
                    var defaultTemplateGroup = await EnsureDefaultTemplateGroupAsync(context, page.OwnerId);
                    if (defaultTemplateGroup is null)
                    {
                        throw new InvalidOperationException("Template-Gruppe konnte für den aktuellen Benutzer nicht ermittelt werden.");
                    }

                    page.TemplateGroupId = await ResolveTargetTemplateGroupIdAsync(
                        context,
                        page.OwnerId,
                        page.TemplateGroupId,
                        defaultTemplateGroup.Id);
                }

                page.CreatedAt = DateTime.UtcNow;
                 context.WikiPages.Add(page);
            }
            else
            {
                // Update eines bestehenden Eintrags
                var existingPage = await context.WikiPages
                    .FirstOrDefaultAsync(existingPage => existingPage.Id == page.Id);

                if (existingPage is null)
                {
                    throw new InvalidOperationException($"Wiki-Seite mit der ID {page.Id} wurde nicht gefunden.");
                }

                var createdAt = existingPage.CreatedAt;
                var authorId = existingPage.AuthorId;

                context.Entry(existingPage).CurrentValues.SetValues(page);

                // CreatedAt und AuthorId bleiben beim Bearbeiten unverändert.
                existingPage.CreatedAt = createdAt;
                existingPage.AuthorId = authorId;

                if (page.RowVersion is not null)
                {
                    context.Entry(existingPage).Property(x => x.RowVersion).OriginalValue = page.RowVersion;
                }
            }

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Die Wiki-Seite wurde zwischenzeitlich geändert. Bitte laden Sie die Seite neu.", ex);
            }
        }

        public async Task DeletePageAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // WikiPage nutzt RowVersion als Concurrency-Token.
            // Deshalb müssen wir den Datensatz zuerst laden, damit EF den korrekten
            // RowVersion-Wert beim DELETE verwenden kann.
            var pageToDelete = await context.WikiPages
                .FirstOrDefaultAsync(page => page.Id == id);

            if (pageToDelete is null)
            {
                return;
            }

            context.WikiPages.Remove(pageToDelete);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Eintrag existierte wohl nicht mehr
            }
        }

        #endregion

        #region Favoriten

        public async Task AddOrMoveFavoriteAsync(string userId, int pageId, int? groupId = null)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (!await IsKnownUserAsync(context, userId))
            {
                return;
            }

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return;
            }

            var targetGroupId = await ResolveTargetGroupIdAsync(context, userId, groupId, defaultGroup.Id);

            var existing = await context.WikiPageFavorites
                .FirstOrDefaultAsync(favorite => favorite.UserId == userId && favorite.WikiPageId == pageId);

            if (existing != null)
            {
                if (existing.FavoriteGroupId != targetGroupId)
                {
                    existing.FavoriteGroupId = targetGroupId;
                    await context.SaveChangesAsync();
                }

                await RecordFavoriteUsageAsync(userId, pageId);
                return;
            }

            context.WikiPageFavorites.Add(new WikiPageFavorite
            {
                UserId = userId,
                WikiPageId = pageId,
                FavoriteGroupId = targetGroupId,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            await RecordFavoriteUsageAsync(userId, pageId);
        }

        public async Task AddFavoriteAsync(string userId, int pageId, int? groupId = null)
        {
            await AddOrMoveFavoriteAsync(userId, pageId, groupId);
        }

        public async Task AssignFavoriteToGroupAsync(string userId, int pageId, int groupId)
        {
            await AddOrMoveFavoriteAsync(userId, pageId, groupId);
        }

        public async Task RemoveFavoriteAsync(string userId, int pageId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (!await IsKnownUserAsync(context, userId))
            {
                return;
            }

            var favorite = await context.WikiPageFavorites
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.WikiPageId == pageId);

            if (favorite is null)
            {
                return;
            }

            context.WikiPageFavorites.Remove(favorite);
            await context.SaveChangesAsync();
        }

        public async Task<List<WikiPageFavorite>> GetFavoritesAsync(string userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return new List<WikiPageFavorite>();
            }

            var favorites = await context.WikiPageFavorites
                .Include(favorite => favorite.FavoriteGroup)
                .Include(favorite => favorite.WikiPage)
                .ThenInclude(page => page!.Category)
                .Include(favorite => favorite.WikiPage)
                .ThenInclude(page => page!.AttributeValues)
                .ThenInclude(value => value.AttributeDefinition)
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .ToListAsync();

            var pages = favorites
                .Where(favorite => favorite.WikiPage != null)
                .Select(favorite => favorite.WikiPage!)
                .GroupBy(page => page.Id)
                .Select(group => group.First())
                .ToList();

            await MapWikiPageUserDisplayNamesAsync(context, pages);

            return favorites;
        }

        public async Task<List<WikiPageFavorite>> GetFavoritesByGroupAsync(string userId, int groupId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return new List<WikiPageFavorite>();
            }

            var targetGroupId = await ResolveTargetGroupIdAsync(context, userId, groupId, defaultGroup.Id);

            var favorites = await context.WikiPageFavorites
                .Include(favorite => favorite.FavoriteGroup)
                .Include(favorite => favorite.WikiPage)
                .ThenInclude(page => page!.Category)
                .Include(favorite => favorite.WikiPage)
                .ThenInclude(page => page!.AttributeValues)
                .ThenInclude(value => value.AttributeDefinition)
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId && favorite.FavoriteGroupId == targetGroupId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .ToListAsync();

            var pages = favorites
                .Where(favorite => favorite.WikiPage != null)
                .Select(favorite => favorite.WikiPage!)
                .GroupBy(page => page.Id)
                .Select(group => group.First())
                .ToList();

            await MapWikiPageUserDisplayNamesAsync(context, pages);

            return favorites;
        }

        public async Task<Dictionary<int, List<WikiPageFavorite>>> GetFavoritesGroupedAsync(string userId)
        {
            var favorites = await GetFavoritesAsync(userId);
            return favorites
                .GroupBy(favorite => favorite.FavoriteGroupId)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
        }

        public async Task<List<WikiFavoriteGroup>> GetFavoriteGroupsAsync(string userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return new List<WikiFavoriteGroup>();
            }

            return await context.WikiFavoriteGroups
                .AsNoTracking()
                .Where(group => group.UserId == userId)
                .OrderBy(group => group.Name)
                .ToListAsync();
        }

        public async Task<WikiFavoriteGroup> CreateFavoriteGroupAsync(string userId, string name)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                throw new InvalidOperationException("Benutzerkonto wurde nicht gefunden.");
            }

            var normalizedName = name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("Gruppenname darf nicht leer sein.");
            }

            var exists = await context.WikiFavoriteGroups
                .AnyAsync(group => group.UserId == userId && group.Name == normalizedName);

            if (exists)
            {
                throw new InvalidOperationException("Eine Favoriten-Gruppe mit diesem Namen existiert bereits.");
            }

            var group = new WikiFavoriteGroup
            {
                UserId = userId,
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow
            };

            context.WikiFavoriteGroups.Add(group);
            await context.SaveChangesAsync();

            return group;
        }

        public async Task<WikiFavoriteGroup> RenameFavoriteGroupAsync(string userId, int groupId, string name)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                throw new InvalidOperationException("Benutzerkonto wurde nicht gefunden.");
            }

            var normalizedName = name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("Gruppenname darf nicht leer sein.");
            }

            var group = await context.WikiFavoriteGroups
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == groupId);

            if (group is null)
            {
                throw new InvalidOperationException("Die Favoriten-Gruppe wurde nicht gefunden.");
            }

            if (group.Id == defaultGroup.Id)
            {
                throw new InvalidOperationException("Die Standardgruppe kann nicht umbenannt werden.");
            }

            var nameAlreadyUsed = await context.WikiFavoriteGroups
                .AnyAsync(entry => entry.UserId == userId && entry.Id != groupId && entry.Name == normalizedName);

            if (nameAlreadyUsed)
            {
                throw new InvalidOperationException("Eine Favoriten-Gruppe mit diesem Namen existiert bereits.");
            }

            group.Name = normalizedName;
            await context.SaveChangesAsync();

            return group;
        }

        public async Task DeleteFavoriteGroupAsync(string userId, int groupId, bool preventDeletionIfNotEmpty = false)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultFavoriteGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return;
            }

            if (defaultGroup.Id == groupId)
            {
                throw new InvalidOperationException("Die Standardgruppe kann nicht gelöscht werden.");
            }

            var group = await context.WikiFavoriteGroups
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == groupId);

            if (group is null)
            {
                return;
            }

            var favoritesToMove = await context.WikiPageFavorites
                .Where(favorite => favorite.UserId == userId && favorite.FavoriteGroupId == groupId)
                .ToListAsync();

            if (preventDeletionIfNotEmpty && favoritesToMove.Count > 0)
            {
                throw new InvalidOperationException("Die Gruppe kann nicht gelöscht werden, solange sie Favoriten enthält.");
            }

            foreach (var favorite in favoritesToMove)
            {
                favorite.FavoriteGroupId = defaultGroup.Id;
            }

            context.WikiFavoriteGroups.Remove(group);
            await context.SaveChangesAsync();
        }



        public async Task AssignTemplateToGroupAsync(string userId, int pageId, int groupId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultTemplateGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return;
            }

            var targetGroupId = await ResolveTargetTemplateGroupIdAsync(context, userId, groupId, defaultGroup.Id);
            var page = await context.WikiPages
                .FirstOrDefaultAsync(entry => entry.Id == pageId && entry.Status == WikiPageStatus.Template);

            if (page is null)
            {
                return;
            }

            page.TemplateGroupId = targetGroupId;
            await context.SaveChangesAsync();
        }

        public async Task<List<WikiTemplateGroup>> GetTemplateGroupsAsync(string userId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultTemplateGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return new List<WikiTemplateGroup>();
            }

            return await context.WikiTemplateGroups
                .AsNoTracking()
                .Where(group => group.UserId == userId)
                .OrderBy(group => group.Name)
                .ToListAsync();
        }

        public async Task<WikiTemplateGroup> CreateTemplateGroupAsync(string userId, string name)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultTemplateGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                throw new InvalidOperationException("Unbekannter Benutzer.");
            }

            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("Ein Gruppenname ist erforderlich.");
            }

            var exists = await context.WikiTemplateGroups
                .AnyAsync(entry => entry.UserId == userId && entry.Name == normalizedName);

            if (exists)
            {
                throw new InvalidOperationException("Eine Vorlagen-Gruppe mit diesem Namen existiert bereits.");
            }

            var group = new WikiTemplateGroup
            {
                UserId = userId,
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow
            };

            context.WikiTemplateGroups.Add(group);
            await context.SaveChangesAsync();

            return group;
        }

        public async Task<WikiTemplateGroup> RenameTemplateGroupAsync(string userId, int groupId, string name)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultTemplateGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                throw new InvalidOperationException("Unbekannter Benutzer.");
            }

            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("Ein Gruppenname ist erforderlich.");
            }

            var group = await context.WikiTemplateGroups
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == groupId);

            if (group is null)
            {
                throw new InvalidOperationException("Die Vorlagen-Gruppe wurde nicht gefunden.");
            }

            if (group.Id == defaultGroup.Id)
            {
                throw new InvalidOperationException("Die Standardgruppe kann nicht umbenannt werden.");
            }

            var nameAlreadyUsed = await context.WikiTemplateGroups
                .AnyAsync(entry => entry.UserId == userId && entry.Id != groupId && entry.Name == normalizedName);

            if (nameAlreadyUsed)
            {
                throw new InvalidOperationException("Eine Vorlagen-Gruppe mit diesem Namen existiert bereits.");
            }

            group.Name = normalizedName;
            await context.SaveChangesAsync();

            return group;
        }

        public async Task DeleteTemplateGroupAsync(string userId, int groupId, bool preventDeletionIfNotEmpty = false)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var defaultGroup = await EnsureDefaultTemplateGroupAsync(context, userId);
            if (defaultGroup is null)
            {
                return;
            }

            if (defaultGroup.Id == groupId)
            {
                throw new InvalidOperationException("Die Standardgruppe kann nicht gelöscht werden.");
            }

            var group = await context.WikiTemplateGroups
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == groupId);

            if (group is null)
            {
                return;
            }

            var templatesToMove = await context.WikiPages
                .Where(page => page.Status == WikiPageStatus.Template && page.TemplateGroupId == groupId)
                .ToListAsync();

            if (preventDeletionIfNotEmpty && templatesToMove.Count > 0)
            {
                throw new InvalidOperationException("Die Gruppe kann nicht gelöscht werden, solange sie Vorlagen enthält.");
            }

            foreach (var template in templatesToMove)
            {
                template.TemplateGroupId = defaultGroup.Id;
            }

            context.WikiTemplateGroups.Remove(group);
            await context.SaveChangesAsync();
        }

        private static async Task<WikiFavoriteGroup?> EnsureDefaultFavoriteGroupAsync(ApplicationDbContext context, string userId)
        {
            if (!await IsKnownUserAsync(context, userId))
            {
                return null;
            }

            var defaultGroup = await context.WikiFavoriteGroups
                .FirstOrDefaultAsync(group => group.UserId == userId && group.Name == WikiFavoriteGroup.DefaultGroupName);

            if (defaultGroup != null)
            {
                return defaultGroup;
            }

            defaultGroup = new WikiFavoriteGroup
            {
                UserId = userId,
                Name = WikiFavoriteGroup.DefaultGroupName,
                CreatedAt = DateTime.UtcNow
            };

            context.WikiFavoriteGroups.Add(defaultGroup);
            await context.SaveChangesAsync();

            return defaultGroup;
        }

        private static async Task<int> ResolveTargetGroupIdAsync(ApplicationDbContext context, string userId, int? requestedGroupId, int defaultGroupId)
        {
            var targetGroupId = requestedGroupId ?? defaultGroupId;

            var hasGroup = await context.WikiFavoriteGroups
                .AnyAsync(group => group.UserId == userId && group.Id == targetGroupId);

            return hasGroup ? targetGroupId : defaultGroupId;
        }


        private static async Task<WikiTemplateGroup?> EnsureDefaultTemplateGroupAsync(ApplicationDbContext context, string userId)
        {
            if (!await IsKnownUserAsync(context, userId))
            {
                return null;
            }

            var defaultGroup = await context.WikiTemplateGroups
                .FirstOrDefaultAsync(group => group.UserId == userId && group.Name == WikiTemplateGroup.DefaultGroupName);

            if (defaultGroup != null)
            {
                return defaultGroup;
            }

            defaultGroup = new WikiTemplateGroup
            {
                UserId = userId,
                Name = WikiTemplateGroup.DefaultGroupName,
                CreatedAt = DateTime.UtcNow
            };

            context.WikiTemplateGroups.Add(defaultGroup);
            await context.SaveChangesAsync();

            return defaultGroup;
        }

        private static async Task<int> ResolveTargetTemplateGroupIdAsync(ApplicationDbContext context, string userId, int? requestedGroupId, int defaultGroupId)
        {
            var targetGroupId = requestedGroupId ?? defaultGroupId;

            var hasGroup = await context.WikiTemplateGroups
                .AnyAsync(group => group.UserId == userId && group.Id == targetGroupId);

            return hasGroup ? targetGroupId : defaultGroupId;
        }

        private static async Task<bool> IsKnownUserAsync(ApplicationDbContext context, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return await context.Users.AnyAsync(user => user.Id == userId);
        }

        #endregion

        #region Attribute

        public async Task<List<WikiAttributeDefinition>> GetAttributeDefinitionsAsync()
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiAttributeDefinitions
                .AsNoTracking()
                .OrderBy(definition => definition.Name)
                .ToListAsync();
        }

        public async Task<WikiAttributeDefinition?> GetAttributeDefinitionByIdAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiAttributeDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(definition => definition.Id == id);
        }

        public async Task SaveAttributeDefinitionAsync(WikiAttributeDefinition definition)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (definition.Id == 0)
            {
                context.WikiAttributeDefinitions.Add(definition);
            }
            else
            {
                context.WikiAttributeDefinitions.Update(definition);
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAttributeDefinitionAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var definition = new WikiAttributeDefinition { Id = id };
            context.Entry(definition).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        public async Task<List<WikiPageAttributeValue>> GetAttributeValuesAsync(int pageId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiPageAttributeValues
                .Include(value => value.AttributeDefinition)
                .AsNoTracking()
                .Where(value => value.WikiPageId == pageId)
                .OrderBy(value => value.AttributeDefinition!.Name)
                .ToListAsync();
        }

        public async Task SaveAttributeValueAsync(WikiPageAttributeValue value)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (value.Id == 0)
            {
                context.WikiPageAttributeValues.Add(value);
            }
            else
            {
                context.WikiPageAttributeValues.Update(value);
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAttributeValueAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var value = new WikiPageAttributeValue { Id = id };
            context.Entry(value).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        #endregion

        #region Kommentare

        public async Task<List<WikiComment>> GetCommentsAsync(int pageId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiComments
                .AsNoTracking()
                .Where(comment => comment.WikiPageId == pageId)
                .OrderByDescending(comment => comment.CreatedAt)
                .ToListAsync();
        }

        public async Task<WikiComment?> GetCommentByIdAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiComments
                .AsNoTracking()
                .FirstOrDefaultAsync(comment => comment.Id == id);
        }

        public async Task SaveCommentAsync(WikiComment comment)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (comment.Id == 0)
            {
                comment.CreatedAt = DateTime.UtcNow;
                context.WikiComments.Add(comment);
            }
            else
            {
                context.WikiComments.Update(comment);
                context.Entry(comment).Property(entry => entry.CreatedAt).IsModified = false;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteCommentAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var comment = new WikiComment { Id = id };
            context.Entry(comment).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        #endregion

        #region Zuweisungen

        public async Task<List<WikiAssignment>> GetAssignmentsAsync(int pageId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiAssignments
                .AsNoTracking()
                .Where(assignment => assignment.WikiPageId == pageId)
                .OrderByDescending(assignment => assignment.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<WikiAssignment>> GetAssignmentsByAssigneeAsync(string assigneeId, WikiAssignmentStatus? status)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var query = context.WikiAssignments
                .AsNoTracking()
                .Where(assignment => assignment.AssigneeId == assigneeId);

            if (status.HasValue)
            {
                query = query.Where(assignment => assignment.Status == status.Value);
            }

            return await query
                .OrderByDescending(assignment => assignment.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveAssignmentAsync(WikiAssignment assignment)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (assignment.Id == 0)
            {
                var existingAssignment = await context.WikiAssignments
                    .FirstOrDefaultAsync(entry => entry.WikiPageId == assignment.WikiPageId && entry.AssigneeId == assignment.AssigneeId);

                if (existingAssignment is not null)
                {
                    existingAssignment.Status = assignment.Status;
                    await context.SaveChangesAsync();
                    return;
                }

                assignment.CreatedAt = DateTime.UtcNow;
                context.WikiAssignments.Add(assignment);
            }
            else
            {
                context.WikiAssignments.Update(assignment);
                context.Entry(assignment).Property(entry => entry.CreatedAt).IsModified = false;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAssignmentAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var assignment = new WikiAssignment { Id = id };
            context.Entry(assignment).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        #endregion

        #region Änderungsverlauf

        public async Task<List<WikiChangeLog>> GetChangeLogsAsync(int pageId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiChangeLogs
                .AsNoTracking()
                .Where(entry => entry.WikiPageId == pageId)
                .OrderByDescending(entry => entry.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<WikiChangeLog>> GetChangeLogsByAuthorAsync(string authorId)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            return await context.WikiChangeLogs
                .AsNoTracking()
                .Where(entry => entry.AuthorId == authorId)
                .OrderByDescending(entry => entry.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangeLogEntryAsync(WikiChangeLog changeLog)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (changeLog.Id == 0)
            {
                changeLog.CreatedAt = DateTime.UtcNow;
                context.WikiChangeLogs.Add(changeLog);
            }
            else
            {
                context.WikiChangeLogs.Update(changeLog);
                context.Entry(changeLog).Property(entry => entry.CreatedAt).IsModified = false;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteChangeLogEntryAsync(int id)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var entry = new WikiChangeLog { Id = id };
            context.Entry(entry).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        #endregion
    }
}
