using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public class OnboardingService(IDbContextFactory<ApplicationDbContext> dbFactory) : IOnboardingService
{
    public async Task<List<OnboardingProfileListItem>> GetProfilesAsync(
        string? searchText = null,
        OnboardingProfileStatus? status = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = context.OnboardingProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(profile =>
                profile.FullName.Contains(term)
                || (profile.Department != null && profile.Department.Contains(term))
                || (profile.Supervisor != null && profile.Supervisor.Contains(term))
                || (profile.Email != null && profile.Email.Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(profile => profile.Status == status.Value);
        }

        if (onlyOpen)
        {
            query = query.Where(profile => profile.Status != OnboardingProfileStatus.Completed && profile.Status != OnboardingProfileStatus.Archived);
        }

        var items = await query
            .OrderByDescending(profile => profile.LastModifiedAt)
            .Select(profile => new OnboardingProfileListItem
            {
                Id = profile.Id,
                FullName = profile.FullName,
                Department = profile.Department,
                Supervisor = profile.Supervisor,
                Email = profile.Email,
                Status = profile.Status,
                MeasureTotal = profile.MeasureEntries.Count,
                MeasureCompleted = profile.MeasureEntries.Count(entry => entry.IsCompleted),
                ChecklistTotal = profile.ChecklistEntries.Count,
                ChecklistCompleted = profile.ChecklistEntries.Count(entry => entry.IsCompleted),
                LastModifiedAt = profile.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.ProgressPercent = CalculateProgressPercent(item.MeasureCompleted, item.MeasureTotal, item.ChecklistCompleted, item.ChecklistTotal);
        }

        return items;
    }

    public async Task<OnboardingProfile?> GetProfileAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var profile = await context.OnboardingProfiles
            .AsNoTracking()
            .Include(entry => entry.LinkedUser)
            .Include(entry => entry.MeasureEntries)
                .ThenInclude(entry => entry.CatalogItem)
            .Include(entry => entry.ChecklistEntries)
                .ThenInclude(entry => entry.CatalogItem)
            .FirstOrDefaultAsync(entry => entry.Id == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.MeasureEntries = profile.MeasureEntries
            .OrderBy(entry => entry.CatalogItem?.SortOrder ?? int.MaxValue)
            .ThenBy(entry => entry.CreatedAt)
            .ToList();

        profile.ChecklistEntries = profile.ChecklistEntries
            .OrderBy(entry => entry.CatalogItem?.SortOrder ?? int.MaxValue)
            .ThenBy(entry => entry.CreatedAt)
            .ToList();

        return profile;
    }

    public async Task<OnboardingProfile> CreateProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        profile.Id = 0;
        profile.FullName = profile.FullName.Trim();
        profile.CreatedAt = now;
        profile.LastModifiedAt = now;

        if (profile.Status == OnboardingProfileStatus.Completed)
        {
            profile.CompletedAt = now;
        }
        else
        {
            profile.CompletedAt = null;
        }

        context.OnboardingProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);

        var checklistCatalog = await context.OnboardingChecklistCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        if (checklistCatalog.Count > 0)
        {
            foreach (var checklistItem in checklistCatalog)
            {
                context.OnboardingChecklistEntries.Add(new OnboardingChecklistEntry
                {
                    ProfileId = profile.Id,
                    CatalogItemId = checklistItem.Id,
                    IsCompleted = false,
                    Result = OnboardingChecklistResult.Pending,
                    CreatedAt = now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetRequiredProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<OnboardingProfile> UpdateProfileAsync(OnboardingProfile profile, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingProfiles
            .FirstOrDefaultAsync(entry => entry.Id == profile.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Onboarding-Profil mit der ID {profile.Id} wurde nicht gefunden.");

        existing.FullName = profile.FullName.Trim();
        existing.Department = profile.Department?.Trim();
        existing.Supervisor = profile.Supervisor?.Trim();
        existing.Email = profile.Email?.Trim();
        existing.PhoneNumber = profile.PhoneNumber?.Trim();
        existing.PrinterCardNumber = profile.PrinterCardNumber?.Trim();
        existing.LinkedUserId = string.IsNullOrWhiteSpace(profile.LinkedUserId) ? null : profile.LinkedUserId;
        existing.StartDate = profile.StartDate;
        existing.TargetDate = profile.TargetDate;
        existing.Notes = profile.Notes;
        existing.Status = profile.Status;

        var now = DateTime.UtcNow;
        existing.LastModifiedAt = now;

        if (existing.Status == OnboardingProfileStatus.Completed)
        {
            existing.CompletedAt ??= now;
        }
        else
        {
            existing.CompletedAt = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        return await GetRequiredProfileAsync(existing.Id, cancellationToken);
    }

    public async Task DeleteProfileAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var profile = await context.OnboardingProfiles.FirstOrDefaultAsync(entry => entry.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        context.OnboardingProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OnboardingProfile?> DuplicateProfileAsync(int sourceProfileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var source = await context.OnboardingProfiles
            .AsNoTracking()
            .Include(entry => entry.MeasureEntries)
            .Include(entry => entry.ChecklistEntries)
            .FirstOrDefaultAsync(entry => entry.Id == sourceProfileId, cancellationToken);

        if (source is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var clone = new OnboardingProfile
        {
            FullName = $"{source.FullName} (Kopie)",
            Department = source.Department,
            Supervisor = source.Supervisor,
            Email = source.Email,
            PhoneNumber = source.PhoneNumber,
            PrinterCardNumber = source.PrinterCardNumber,
            LinkedUserId = null,
            Status = OnboardingProfileStatus.Draft,
            StartDate = null,
            TargetDate = source.TargetDate,
            CompletedAt = null,
            Notes = source.Notes,
            CreatedAt = now,
            LastModifiedAt = now
        };

        context.OnboardingProfiles.Add(clone);
        await context.SaveChangesAsync(cancellationToken);

        if (source.MeasureEntries.Count > 0)
        {
            foreach (var entry in source.MeasureEntries)
            {
                context.OnboardingMeasureEntries.Add(new OnboardingMeasureEntry
                {
                    ProfileId = clone.Id,
                    CatalogItemId = entry.CatalogItemId,
                    Value = entry.Value,
                    Notes = entry.Notes,
                    IsCompleted = false,
                    CreatedAt = now,
                    CompletedAt = null
                });
            }
        }

        if (source.ChecklistEntries.Count > 0)
        {
            foreach (var entry in source.ChecklistEntries)
            {
                context.OnboardingChecklistEntries.Add(new OnboardingChecklistEntry
                {
                    ProfileId = clone.Id,
                    CatalogItemId = entry.CatalogItemId,
                    IsCompleted = false,
                    Result = OnboardingChecklistResult.Pending,
                    Notes = entry.Notes,
                    CreatedAt = now,
                    CompletedAt = null
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(clone.Id, cancellationToken);
    }

    public async Task<List<OnboardingMeasureCatalogItem>> GetMeasureCatalogAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = context.OnboardingMeasureCatalogItems.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<OnboardingMeasureCatalogItem> SaveMeasureCatalogItemAsync(OnboardingMeasureCatalogItem item, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var normalizedName = item.Name.Trim();

        var duplicateExists = await context.OnboardingMeasureCatalogItems
            .AnyAsync(entry => entry.Id != item.Id && entry.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Ein Maßnahmenfeld mit diesem Namen existiert bereits.");
        }

        if (item.Id == 0)
        {
            item.Name = normalizedName;
            item.CreatedAt = DateTime.UtcNow;
            context.OnboardingMeasureCatalogItems.Add(item);
        }
        else
        {
            var existing = await context.OnboardingMeasureCatalogItems
                .FirstOrDefaultAsync(entry => entry.Id == item.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Maßnahmenfeld mit ID {item.Id} wurde nicht gefunden.");

            existing.Name = normalizedName;
            existing.Description = item.Description?.Trim();
            existing.SortOrder = item.SortOrder;
            existing.IsActive = item.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await context.OnboardingMeasureCatalogItems.AsNoTracking().FirstAsync(entry => entry.Name == normalizedName, cancellationToken);
    }

    public async Task SetMeasureCatalogItemActiveAsync(int catalogItemId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await context.OnboardingMeasureCatalogItems
            .FirstOrDefaultAsync(entry => entry.Id == catalogItemId, cancellationToken);

        if (item is null)
        {
            return;
        }

        item.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<OnboardingChecklistCatalogItem>> GetChecklistCatalogAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = context.OnboardingChecklistCatalogItems.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<OnboardingChecklistCatalogItem> SaveChecklistCatalogItemAsync(OnboardingChecklistCatalogItem item, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var normalizedName = item.Name.Trim();

        var duplicateExists = await context.OnboardingChecklistCatalogItems
            .AnyAsync(entry => entry.Id != item.Id && entry.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Ein Checklistenpunkt mit diesem Namen existiert bereits.");
        }

        if (item.Id == 0)
        {
            item.Name = normalizedName;
            item.CreatedAt = DateTime.UtcNow;
            context.OnboardingChecklistCatalogItems.Add(item);
        }
        else
        {
            var existing = await context.OnboardingChecklistCatalogItems
                .FirstOrDefaultAsync(entry => entry.Id == item.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Checklistenpunkt mit ID {item.Id} wurde nicht gefunden.");

            existing.Name = normalizedName;
            existing.Description = item.Description?.Trim();
            existing.SortOrder = item.SortOrder;
            existing.IsActive = item.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await context.OnboardingChecklistCatalogItems.AsNoTracking().FirstAsync(entry => entry.Name == normalizedName, cancellationToken);
    }

    public async Task SetChecklistCatalogItemActiveAsync(int catalogItemId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await context.OnboardingChecklistCatalogItems
            .FirstOrDefaultAsync(entry => entry.Id == catalogItemId, cancellationToken);

        if (item is null)
        {
            return;
        }

        item.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OnboardingMeasureEntry> AddMeasureEntryAsync(int profileId, int catalogItemId, string value, string? notes, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.OnboardingProfiles.AnyAsync(profile => profile.Id == profileId, cancellationToken))
        {
            throw new InvalidOperationException($"Onboarding-Profil mit ID {profileId} wurde nicht gefunden.");
        }

        if (!await context.OnboardingMeasureCatalogItems.AnyAsync(item => item.Id == catalogItemId, cancellationToken))
        {
            throw new InvalidOperationException($"Maßnahmenfeld mit ID {catalogItemId} wurde nicht gefunden.");
        }

        var entry = new OnboardingMeasureEntry
        {
            ProfileId = profileId,
            CatalogItemId = catalogItemId,
            Value = value.Trim(),
            Notes = notes?.Trim(),
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        context.OnboardingMeasureEntries.Add(entry);
        await TouchProfileAsync(context, profileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await context.OnboardingMeasureEntries
            .AsNoTracking()
            .Include(item => item.CatalogItem)
            .FirstAsync(item => item.Id == entry.Id, cancellationToken);
    }

    public async Task<OnboardingMeasureEntry?> UpdateMeasureEntryAsync(OnboardingMeasureEntry entry, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingMeasureEntries
            .FirstOrDefaultAsync(item => item.Id == entry.Id, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        existing.CatalogItemId = entry.CatalogItemId;
        existing.Value = entry.Value.Trim();
        existing.Notes = entry.Notes?.Trim();

        await TouchProfileAsync(context, existing.ProfileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await context.OnboardingMeasureEntries
            .AsNoTracking()
            .Include(item => item.CatalogItem)
            .FirstOrDefaultAsync(item => item.Id == existing.Id, cancellationToken);
    }

    public async Task<bool> DeleteMeasureEntryAsync(int measureEntryId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingMeasureEntries
            .FirstOrDefaultAsync(item => item.Id == measureEntryId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        var profileId = existing.ProfileId;
        context.OnboardingMeasureEntries.Remove(existing);

        await TouchProfileAsync(context, profileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetMeasureEntryCompletedAsync(int measureEntryId, bool isCompleted, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingMeasureEntries
            .FirstOrDefaultAsync(item => item.Id == measureEntryId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        existing.IsCompleted = isCompleted;
        existing.CompletedAt = isCompleted ? DateTime.UtcNow : null;

        await TouchProfileAsync(context, existing.ProfileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<OnboardingChecklistEntry?> UpdateChecklistEntryAsync(OnboardingChecklistEntry entry, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingChecklistEntries
            .FirstOrDefaultAsync(item => item.Id == entry.Id, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        existing.Result = entry.Result;
        existing.IsCompleted = entry.IsCompleted;
        existing.CompletedAt = entry.IsCompleted ? DateTime.UtcNow : null;
        existing.Notes = entry.Notes?.Trim();

        await TouchProfileAsync(context, existing.ProfileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await context.OnboardingChecklistEntries
            .AsNoTracking()
            .Include(item => item.CatalogItem)
            .FirstOrDefaultAsync(item => item.Id == existing.Id, cancellationToken);
    }

    public async Task<bool> SetChecklistEntryCompletedAsync(int checklistEntryId, bool isCompleted, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingChecklistEntries
            .FirstOrDefaultAsync(item => item.Id == checklistEntryId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        existing.IsCompleted = isCompleted;
        existing.CompletedAt = isCompleted ? DateTime.UtcNow : null;
        if (!isCompleted && existing.Result != OnboardingChecklistResult.Failed)
        {
            existing.Result = OnboardingChecklistResult.Pending;
        }

        await TouchProfileAsync(context, existing.ProfileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> SetChecklistEntryResultAsync(int checklistEntryId, OnboardingChecklistResult result, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.OnboardingChecklistEntries
            .FirstOrDefaultAsync(item => item.Id == checklistEntryId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        existing.Result = result;
        existing.IsCompleted = result is OnboardingChecklistResult.Passed or OnboardingChecklistResult.NotApplicable;
        existing.CompletedAt = existing.IsCompleted ? DateTime.UtcNow : null;

        await TouchProfileAsync(context, existing.ProfileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static int CalculateProgressPercent(int measureCompleted, int measureTotal, int checklistCompleted, int checklistTotal)
    {
        var total = measureTotal + checklistTotal;
        if (total <= 0)
        {
            return 0;
        }

        var done = measureCompleted + checklistCompleted;
        return (int)Math.Round(done * 100d / total, MidpointRounding.AwayFromZero);
    }

    private static async Task TouchProfileAsync(ApplicationDbContext context, int profileId, CancellationToken cancellationToken)
    {
        var profile = await context.OnboardingProfiles.FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        profile.LastModifiedAt = DateTime.UtcNow;
    }

    private async Task<OnboardingProfile> GetRequiredProfileAsync(int profileId, CancellationToken cancellationToken)
    {
        return await GetProfileAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Onboarding-Profil mit ID {profileId} wurde nicht gefunden.");
    }
}
