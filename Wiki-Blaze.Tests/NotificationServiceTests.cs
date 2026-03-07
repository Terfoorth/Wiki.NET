using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;
using Wiki_Blaze.Services;

namespace Wiki_Blaze.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task Scheduler_RespectsBerlinTimeZoneThreshold()
    {
        var (service, factory) = CreateService();
        await SeedWikiReviewSourceAsync(factory, "user-berlin", "Europe/Berlin", 101, new DateTime(2026, 1, 12));

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 7, 6, 59, 0, DateTimeKind.Utc));
        Assert.Equal(0, await CountNotificationsAsync(factory));

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 7, 7, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, await CountNotificationsAsync(factory));
    }

    [Fact]
    public async Task Scheduler_RespectsNewYorkTimeZoneThreshold()
    {
        var (service, factory) = CreateService();
        await SeedWikiReviewSourceAsync(factory, "user-ny", "America/New_York", 102, new DateTime(2026, 1, 12));

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 7, 12, 59, 0, DateTimeKind.Utc));
        Assert.Equal(0, await CountNotificationsAsync(factory));

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 7, 13, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, await CountNotificationsAsync(factory));
    }

    [Fact]
    public async Task Scheduler_IsIdempotentForSameBusinessDay()
    {
        var (service, factory) = CreateService();
        await SeedWikiReviewSourceAsync(factory, "user-idempotent", "Europe/Berlin", 103, new DateTime(2026, 1, 12));

        var runTime = new DateTime(2026, 1, 7, 8, 30, 0, DateTimeKind.Utc);
        await service.GenerateScheduledNotificationsAsync(runTime);
        await service.GenerateScheduledNotificationsAsync(runTime.AddMinutes(1));

        var notifications = await GetNotificationsAsync(factory);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task MarkAsRead_OnlyAffectsOwningUser()
    {
        var (service, factory) = CreateService();

        await using (var context = await factory.CreateDbContextAsync())
        {
            context.Users.AddRange(
                new ApplicationUser { Id = "owner", UserName = "owner", TimeZone = "UTC" },
                new ApplicationUser { Id = "other", UserName = "other", TimeZone = "UTC" });

            context.AppNotifications.Add(new AppNotification
            {
                UserId = "owner",
                Kind = NotificationKind.WikiReviewDate,
                SourceId = 9001,
                DueDate = new DateTime(2026, 1, 12),
                TriggerDate = new DateTime(2026, 1, 7),
                CreatedAtUtc = DateTime.UtcNow,
                IsRead = false,
                Title = "Test",
                Body = "Body",
                TargetUrl = "/wiki/view/9001"
            });

            await context.SaveChangesAsync();
        }

        var notification = (await GetNotificationsAsync(factory)).Single();

        var otherUserResult = await service.MarkAsReadAsync("other", notification.Id);
        Assert.False(otherUserResult);

        var ownerResult = await service.MarkAsReadAsync("owner", notification.Id);
        Assert.True(ownerResult);

        await using var verificationContext = await factory.CreateDbContextAsync();
        var saved = await verificationContext.AppNotifications.SingleAsync(entry => entry.Id == notification.Id);
        Assert.True(saved.IsRead);
        Assert.NotNull(saved.ReadAtUtc);
    }

    [Fact]
    public async Task Scheduler_SkipsInvalidUserSourcesWithoutBlockingValidOnes()
    {
        var (service, factory) = CreateService();
        await SeedWikiReviewSourceAsync(factory, "valid-user", "UTC", 120, new DateTime(2026, 1, 12));
        await SeedOnboardingProfileAsync(
            factory,
            profileId: 5001,
            assignedAgentUserId: "missing-user",
            startDate: new DateTime(2026, 1, 12),
            targetDate: null);

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 7, 8, 30, 0, DateTimeKind.Utc));

        var notifications = await GetNotificationsAsync(factory);
        Assert.Single(notifications);
        Assert.Equal("valid-user", notifications[0].UserId);
    }

    [Fact]
    public async Task Scheduler_CreatesSeparateOnboardingStartAndTargetNotifications()
    {
        var (service, factory) = CreateService();
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.Users.Add(new ApplicationUser
            {
                Id = "onboarding-agent",
                UserName = "onboarding-agent",
                TimeZone = "UTC"
            });
            await context.SaveChangesAsync();
        }

        await SeedOnboardingProfileAsync(
            factory,
            profileId: 5002,
            assignedAgentUserId: "onboarding-agent",
            startDate: new DateTime(2026, 1, 12),
            targetDate: new DateTime(2026, 1, 13));

        await service.GenerateScheduledNotificationsAsync(new DateTime(2026, 1, 8, 8, 30, 0, DateTimeKind.Utc));

        var notifications = await GetNotificationsAsync(factory);
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, item => item.Kind == NotificationKind.OnboardingStartDate);
        Assert.Contains(notifications, item => item.Kind == NotificationKind.OnboardingTargetDate);
    }

    private static (NotificationService Service, TestDbContextFactory Factory) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"notifications-tests-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(options);

        using var context = factory.CreateDbContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var service = new NotificationService(factory, NullLogger<NotificationService>.Instance);
        return (service, factory);
    }

    private static async Task SeedWikiReviewSourceAsync(
        TestDbContextFactory factory,
        string userId,
        string timeZone,
        int pageId,
        DateTime dueDate)
    {
        await using var context = await factory.CreateDbContextAsync();

        if (!await context.Users.AnyAsync(user => user.Id == userId))
        {
            context.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                TimeZone = timeZone
            });
        }

        var reviewDefinition = await context.WikiAttributeDefinitions
            .FirstOrDefaultAsync(definition => definition.Name == "ReviewDate");

        if (reviewDefinition is null)
        {
            reviewDefinition = new WikiAttributeDefinition
            {
                Name = "ReviewDate",
                ValueType = "date"
            };
            context.WikiAttributeDefinitions.Add(reviewDefinition);
            await context.SaveChangesAsync();
        }

        context.WikiPages.Add(new WikiPage
        {
            Id = pageId,
            Title = $"Page {pageId}",
            CategoryId = 1,
            OwnerId = userId,
            Status = WikiPageStatus.Published,
            Visibility = WikiPageVisibility.Public,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        });

        context.WikiPageAttributeValues.Add(new WikiPageAttributeValue
        {
            WikiPageId = pageId,
            AttributeDefinitionId = reviewDefinition.Id,
            Value = dueDate.ToString("yyyy-MM-dd")
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedOnboardingProfileAsync(
        TestDbContextFactory factory,
        int profileId,
        string assignedAgentUserId,
        DateTime? startDate,
        DateTime? targetDate)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.OnboardingProfiles.Add(new OnboardingProfile
        {
            Id = profileId,
            FullName = $"Profile {profileId}",
            EntryDate = new DateTime(2026, 1, 1),
            AssignedAgentUserId = assignedAgentUserId,
            StartDate = startDate,
            TargetDate = targetDate,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<int> CountNotificationsAsync(TestDbContextFactory factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.AppNotifications.CountAsync();
    }

    private static async Task<List<AppNotification>> GetNotificationsAsync(TestDbContextFactory factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.AppNotifications.OrderBy(entry => entry.Id).ToListAsync();
    }
}
