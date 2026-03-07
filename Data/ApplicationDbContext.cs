using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<WikiCategory> WikiCategories { get; set; }
        public DbSet<WikiPage> WikiPages { get; set; }
        public DbSet<WikiAttributeDefinition> WikiAttributeDefinitions { get; set; }
        public DbSet<WikiPageAttributeValue> WikiPageAttributeValues { get; set; }
        public DbSet<WikiPageFavorite> WikiPageFavorites { get; set; }
        public DbSet<WikiFavoriteGroup> WikiFavoriteGroups { get; set; }
        public DbSet<WikiTemplateGroup> WikiTemplateGroups { get; set; }
        public DbSet<WikiAttributeTemplate> WikiAttributeTemplates { get; set; }
        public DbSet<WikiAttributeTemplateAttribute> WikiAttributeTemplateAttributes { get; set; }
        public DbSet<WikiComment> WikiComments { get; set; }
        public DbSet<WikiAssignment> WikiAssignments { get; set; }
        public DbSet<WikiChangeLog> WikiChangeLogs { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<ReminderEmailDispatch> ReminderEmailDispatches { get; set; }
        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

        public DbSet<OnboardingProfile> OnboardingProfiles { get; set; }
        public DbSet<OnboardingMeasureCatalogItem> OnboardingMeasureCatalogItems { get; set; }
        public DbSet<OnboardingMeasureEntry> OnboardingMeasureEntries { get; set; }
        public DbSet<OnboardingChecklistCatalogItem> OnboardingChecklistCatalogItems { get; set; }
        public DbSet<OnboardingChecklistEntry> OnboardingChecklistEntries { get; set; }
        public DbSet<OnboardingProfileAttachment> OnboardingProfileAttachments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(user =>
            {
                user.Property(u => u.ThemePreference)
                    .HasMaxLength(20)
                    .HasDefaultValue(ApplicationUser.DefaultThemePreference);

                user.Property(u => u.DensityPreference)
                    .HasMaxLength(20)
                    .HasDefaultValue(ApplicationUser.DefaultDensityPreference);

                user.Property(u => u.PreferredLanguage)
                    .HasMaxLength(80)
                    .HasDefaultValue(ApplicationUser.DefaultPreferredLanguage);

                user.Property(u => u.TimeZone)
                    .HasMaxLength(120)
                    .HasDefaultValue("UTC");

                user.Property(u => u.StartPage)
                    .HasMaxLength(80)
                    .HasDefaultValue(ApplicationUser.DefaultStartPage);

                user.Property(u => u.ReceiveProductUpdates)
                    .HasDefaultValue(true);

                user.Property(u => u.ReceiveWeeklyDigest)
                    .HasDefaultValue(false);

                user.Property(u => u.ReceiveReminderEmails)
                    .HasDefaultValue(true);
            });

            builder.Entity<UserNotification>(notification =>
            {
                notification.Property(item => item.UserId)
                    .HasMaxLength(450)
                    .IsRequired();

                notification.Property(item => item.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                notification.Property(item => item.Message)
                    .HasMaxLength(1000)
                    .IsRequired();

                notification.Property(item => item.TargetUrl)
                    .HasMaxLength(300);

                notification.HasIndex(item => new
                {
                    item.UserId,
                    item.Type,
                    item.SourceEntityId,
                    item.StageDaysBefore,
                    item.DueDate
                }).IsUnique();

                notification.HasIndex(item => new { item.UserId, item.IsArchived, item.IsRead });

                notification.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ReminderEmailDispatch>(dispatch =>
            {
                dispatch.Property(item => item.UserId)
                    .HasMaxLength(450)
                    .IsRequired();

                dispatch.Property(item => item.LastError)
                    .HasMaxLength(2000);

                dispatch.HasIndex(item => new
                {
                    item.UserId,
                    item.Type,
                    item.SourceEntityId,
                    item.StageDaysBefore,
                    item.DueDate
                }).IsUnique();

                dispatch.HasIndex(item => new { item.SentAtUtc, item.LastAttemptAtUtc });

                dispatch.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AdminAuditLog>(audit =>
            {
                audit.Property(item => item.AdminUserId)
                    .HasMaxLength(450)
                    .IsRequired();

                audit.Property(item => item.Action)
                    .HasMaxLength(120)
                    .IsRequired();

                audit.Property(item => item.Subject)
                    .HasMaxLength(160)
                    .IsRequired();

                audit.Property(item => item.Details)
                    .HasMaxLength(4000);

                audit.HasIndex(item => item.CreatedAtUtc);

                audit.HasOne(item => item.AdminUser)
                    .WithMany()
                    .HasForeignKey(item => item.AdminUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WikiPage>()
                .Property(page => page.Status)
                .HasDefaultValue(WikiPageStatus.Published)
                .HasSentinel((WikiPageStatus)(-1));

            builder.Entity<WikiPage>()
                .Property(page => page.Visibility)
                .HasDefaultValue(WikiPageVisibility.Public)
                .HasSentinel((WikiPageVisibility)(-1));

            builder.Entity<WikiPage>()
                .Property(page => page.EntryType)
                .HasDefaultValue(WikiEntryType.Standard)
                .HasSentinel((WikiEntryType)(-1));

            builder.Entity<WikiPageAttributeValue>()
                .HasIndex(value => new { value.WikiPageId, value.AttributeDefinitionId })
                .IsUnique();

            builder.Entity<WikiPageFavorite>()
                .HasIndex(favorite => new { favorite.UserId, favorite.WikiPageId, favorite.FavoriteGroupId })
                .IsUnique();

            builder.Entity<WikiFavoriteGroup>()
                .HasIndex(group => new { group.UserId, group.Name })
                .IsUnique();

            builder.Entity<WikiTemplateGroup>()
                .HasIndex(group => new { group.UserId, group.Name })
                .IsUnique();

            builder.Entity<WikiPageFavorite>()
                .HasOne(favorite => favorite.WikiPage)
                .WithMany()
                .HasForeignKey(favorite => favorite.WikiPageId);

            builder.Entity<WikiPageFavorite>()
                .HasOne(favorite => favorite.User)
                .WithMany()
                .HasForeignKey(favorite => favorite.UserId);

            builder.Entity<WikiPageFavorite>()
                .HasOne(favorite => favorite.FavoriteGroup)
                .WithMany(group => group.Favorites)
                .HasForeignKey(favorite => favorite.FavoriteGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WikiFavoriteGroup>()
                .HasOne(group => group.User)
                .WithMany()
                .HasForeignKey(group => group.UserId);

            builder.Entity<WikiTemplateGroup>()
                .HasOne(group => group.User)
                .WithMany()
                .HasForeignKey(group => group.UserId);

            builder.Entity<WikiPage>()
                .HasOne(page => page.TemplateGroup)
                .WithMany(group => group.Templates)
                .HasForeignKey(page => page.TemplateGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<WikiAttributeTemplateAttribute>()
                .HasIndex(templateAttribute => new { templateAttribute.TemplateId, templateAttribute.AttributeDefinitionId })
                .IsUnique();

            builder.Entity<WikiComment>()
                .HasIndex(comment => new { comment.WikiPageId, comment.CreatedAt });

            builder.Entity<WikiComment>()
                .HasOne(comment => comment.WikiPage)
                .WithMany(page => page.Comments)
                .HasForeignKey(comment => comment.WikiPageId);

            builder.Entity<WikiComment>()
                .HasOne(comment => comment.Author)
                .WithMany()
                .HasForeignKey(comment => comment.AuthorId);

            builder.Entity<WikiAssignment>()
                .HasIndex(assignment => new { assignment.WikiPageId, assignment.AssigneeId })
                .IsUnique();

            builder.Entity<WikiAssignment>()
                .HasOne(assignment => assignment.WikiPage)
                .WithMany(page => page.Assignments)
                .HasForeignKey(assignment => assignment.WikiPageId);

            builder.Entity<WikiAssignment>()
                .HasOne(assignment => assignment.Assignee)
                .WithMany()
                .HasForeignKey(assignment => assignment.AssigneeId);

            builder.Entity<WikiChangeLog>()
                .HasIndex(changeLog => new { changeLog.WikiPageId, changeLog.CreatedAt });

            builder.Entity<WikiChangeLog>()
                .HasOne(changeLog => changeLog.WikiPage)
                .WithMany(page => page.ChangeLogs)
                .HasForeignKey(changeLog => changeLog.WikiPageId);

            builder.Entity<WikiChangeLog>()
                .HasOne(changeLog => changeLog.Author)
                .WithMany()
                .HasForeignKey(changeLog => changeLog.AuthorId);

            builder.Entity<OnboardingProfile>()
                .Property(profile => profile.Salutation)
                .HasDefaultValue(OnboardingSalutation.Unspecified)
                .HasSentinel((OnboardingSalutation)(-1));

            builder.Entity<OnboardingProfile>()
                .Property(profile => profile.Status)
                .HasDefaultValue(OnboardingProfileStatus.InProgress)
                .HasSentinel((OnboardingProfileStatus)(-1));

            builder.Entity<OnboardingProfile>()
                .HasIndex(profile => new { profile.FullName, profile.Department });

            builder.Entity<OnboardingProfile>()
                .HasIndex(profile => profile.LinkedUserId);

            builder.Entity<OnboardingProfile>()
                .HasIndex(profile => profile.AssignedAgentUserId);

            builder.Entity<OnboardingProfile>()
                .HasIndex(profile => new { profile.LastName, profile.FirstName });

            builder.Entity<OnboardingProfile>()
                .HasOne(profile => profile.LinkedUser)
                .WithMany()
                .HasForeignKey(profile => profile.LinkedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<OnboardingProfile>()
                .HasOne(profile => profile.AssignedAgentUser)
                .WithMany()
                .HasForeignKey(profile => profile.AssignedAgentUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<OnboardingProfileAttachment>()
                .HasIndex(attachment => attachment.ProfileId)
                .IsUnique();

            builder.Entity<OnboardingProfileAttachment>()
                .HasOne(attachment => attachment.Profile)
                .WithOne(profile => profile.Attachment)
                .HasForeignKey<OnboardingProfileAttachment>(attachment => attachment.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OnboardingProfileAttachment>()
                .HasOne(attachment => attachment.UploadedByUser)
                .WithMany()
                .HasForeignKey(attachment => attachment.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<OnboardingMeasureCatalogItem>()
                .HasIndex(item => item.Name)
                .IsUnique();

            builder.Entity<OnboardingMeasureCatalogItem>()
                .Property(item => item.IsActive)
                .HasDefaultValue(true);

            builder.Entity<OnboardingChecklistCatalogItem>()
                .HasIndex(item => item.Name)
                .IsUnique();

            builder.Entity<OnboardingChecklistCatalogItem>()
                .Property(item => item.IsActive)
                .HasDefaultValue(true);

            builder.Entity<OnboardingMeasureEntry>()
                .HasIndex(entry => new { entry.ProfileId, entry.CatalogItemId });

            builder.Entity<OnboardingMeasureEntry>()
                .HasOne(entry => entry.Profile)
                .WithMany(profile => profile.MeasureEntries)
                .HasForeignKey(entry => entry.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OnboardingMeasureEntry>()
                .HasOne(entry => entry.CatalogItem)
                .WithMany(item => item.Entries)
                .HasForeignKey(entry => entry.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OnboardingChecklistEntry>()
                .HasIndex(entry => new { entry.ProfileId, entry.CatalogItemId })
                .IsUnique();

            builder.Entity<OnboardingChecklistEntry>()
                .Property(entry => entry.Result)
                .HasDefaultValue(OnboardingChecklistResult.Pending)
                .HasSentinel((OnboardingChecklistResult)(-1));

            builder.Entity<OnboardingChecklistEntry>()
                .HasOne(entry => entry.Profile)
                .WithMany(profile => profile.ChecklistEntries)
                .HasForeignKey(entry => entry.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OnboardingChecklistEntry>()
                .HasOne(entry => entry.CatalogItem)
                .WithMany(item => item.Entries)
                .HasForeignKey(entry => entry.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            var onboardingSeedDate = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);

            builder.Entity<OnboardingMeasureCatalogItem>().HasData(
                new OnboardingMeasureCatalogItem { Id = 1, Name = "Shared-Drives", Description = "Freigaben und Laufwerkszuordnungen", IsActive = true, SortOrder = 1, CreatedAt = onboardingSeedDate },
                new OnboardingMeasureCatalogItem { Id = 2, Name = "E-Mail Postfächer", Description = "Mailboxen und Berechtigungen", IsActive = true, SortOrder = 2, CreatedAt = onboardingSeedDate },
                new OnboardingMeasureCatalogItem { Id = 3, Name = "Verteilerlisten", Description = "Mailverteiler und Teams-Gruppen", IsActive = true, SortOrder = 3, CreatedAt = onboardingSeedDate },
                new OnboardingMeasureCatalogItem { Id = 4, Name = "Anwendungen", Description = "Fachapplikationen und Lizenzen", IsActive = true, SortOrder = 4, CreatedAt = onboardingSeedDate },
                new OnboardingMeasureCatalogItem { Id = 5, Name = "Berechtigungen", Description = "Rollen und Rechte", IsActive = true, SortOrder = 5, CreatedAt = onboardingSeedDate },
                new OnboardingMeasureCatalogItem { Id = 6, Name = "Sonstiges", Description = "Weitere individuelle Maßnahmen", IsActive = true, SortOrder = 6, CreatedAt = onboardingSeedDate }
            );

            builder.Entity<OnboardingChecklistCatalogItem>().HasData(
                new OnboardingChecklistCatalogItem { Id = 1, Name = "Outlook", Description = "Outlook Anmeldung und Mailversand testen", IsActive = true, SortOrder = 1, CreatedAt = onboardingSeedDate },
                new OnboardingChecklistCatalogItem { Id = 2, Name = "Teams", Description = "Teams Login und Telefonie testen", IsActive = true, SortOrder = 2, CreatedAt = onboardingSeedDate },
                new OnboardingChecklistCatalogItem { Id = 3, Name = "Druckerkarte", Description = "Druckerkarte/FollowMe am Gerät testen", IsActive = true, SortOrder = 3, CreatedAt = onboardingSeedDate }
            );

            // Initiales Seeding für Kategorien (Beispiel-Daten)
            builder.Entity<WikiCategory>().HasData(
                new WikiCategory { Id = 1, Name = "Allgemein", Description = "Generelle Informationen", ParentId = null },
                new WikiCategory { Id = 2, Name = "IT & Entwicklung", Description = "Softwareentwicklung und Infrastruktur", ParentId = null },
                new WikiCategory { Id = 3, Name = "Web Entwicklung", Description = "Blazor, ASP.NET Core, JavaScript", ParentId = 2 },
                new WikiCategory { Id = 4, Name = "Datenbanken", Description = "SQL Server, PostgreSQL", ParentId = 2 },
                new WikiCategory { Id = 5, Name = "Wiki.Forms", Description = "Interaktive PDF-Formulare", ParentId = null, IsFormCategory = true }
            );

            builder.Entity<WikiAttributeDefinition>().HasData(
                new WikiAttributeDefinition
                {
                    Id = 1,
                    Name = "Owner",
                    Description = "Verantwortliche Person",
                    ValueType = "text",
                    IsRequired = true,
                    IsAutoGenerated = false
                },
                new WikiAttributeDefinition
                {
                    Id = 2,
                    Name = "ReviewDate",
                    Description = "Nächstes Review-Datum",
                    ValueType = "date",
                    IsRequired = false,
                    IsAutoGenerated = false
                },
                new WikiAttributeDefinition
                {
                    Id = 3,
                    Name = "Keywords",
                    Description = "Such-Schlagwörter",
                    ValueType = "text",
                    IsRequired = false,
                    IsAutoGenerated = false
                }
            );

            builder.Entity<WikiAttributeTemplate>().HasData(
                new WikiAttributeTemplate
                {
                    Id = 1,
                    Name = "Standard",
                    Description = "Standardvorlage für neue Wiki-Seiten"
                }
            );

            builder.Entity<WikiAttributeTemplateAttribute>().HasData(
                new WikiAttributeTemplateAttribute
                {
                    Id = 1,
                    TemplateId = 1,
                    AttributeDefinitionId = 1,
                    SortOrder = 1,
                    IsRequired = true
                },
                new WikiAttributeTemplateAttribute
                {
                    Id = 2,
                    TemplateId = 1,
                    AttributeDefinitionId = 2,
                    SortOrder = 2,
                    IsRequired = false
                },
                new WikiAttributeTemplateAttribute
                {
                    Id = 3,
                    TemplateId = 1,
                    AttributeDefinitionId = 3,
                    SortOrder = 3,
                    IsRequired = false
                }
            );
        }
    }
}
