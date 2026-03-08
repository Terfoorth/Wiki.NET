using System.IO;
using Microsoft.EntityFrameworkCore;
using Wiki_Blaze.Data;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Services;

public partial class OnboardingService(IDbContextFactory<ApplicationDbContext> dbFactory) : IOnboardingService
{
    private const int MaxAttachmentSizeBytes = 15 * 1024 * 1024;

    private static readonly Dictionary<string, string> AttachmentContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.ms-word",
        "application/x-msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private IDbContextFactory<ApplicationDbContext> DbFactory => dbFactory;

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
                || profile.FirstName.Contains(term)
                || profile.LastName.Contains(term)
                || (profile.Username != null && profile.Username.Contains(term))
                || (profile.TicketNumber != null && profile.TicketNumber.Contains(term))
                || (profile.Department != null && profile.Department.Contains(term))
                || (profile.JobTitle != null && profile.JobTitle.Contains(term))
                || (profile.Location != null && profile.Location.Contains(term))
                || (profile.Supervisor != null && profile.Supervisor.Contains(term))
                || (profile.Email != null && profile.Email.Contains(term))
                || (profile.Hostname != null && profile.Hostname.Contains(term))
                || (profile.DeviceNumber != null && profile.DeviceNumber.Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(profile => profile.Status == status.Value);
        }

        if (onlyOpen)
        {
            query = query.Where(profile => profile.Status != OnboardingProfileStatus.Completed && profile.Status != OnboardingProfileStatus.Archived);
        }

        var rawItems = await query
            .OrderByDescending(profile => profile.LastModifiedAt)
            .Select(profile => new
            {
                Id = profile.Id,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                FullName = profile.FullName,
                Username = profile.Username,
                Department = profile.Department,
                JobTitle = profile.JobTitle,
                Supervisor = profile.Supervisor,
                Email = profile.Email,
                EntryDate = profile.EntryDate,
                AssignedDisplayName = profile.AssignedAgentUser != null ? profile.AssignedAgentUser.DisplayName : null,
                AssignedFirstName = profile.AssignedAgentUser != null ? profile.AssignedAgentUser.FirstName : null,
                AssignedLastName = profile.AssignedAgentUser != null ? profile.AssignedAgentUser.LastName : null,
                AssignedUserName = profile.AssignedAgentUser != null ? profile.AssignedAgentUser.UserName : null,
                AssignedEmail = profile.AssignedAgentUser != null ? profile.AssignedAgentUser.Email : null,
                Status = profile.Status,
                MeasureTotal = profile.MeasureEntries.Count,
                MeasureCompleted = profile.MeasureEntries.Count(entry => entry.IsCompleted),
                ChecklistTotal = profile.ChecklistEntries.Count,
                ChecklistCompleted = profile.ChecklistEntries.Count(entry => entry.IsCompleted),
                LastModifiedAt = profile.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item =>
            {
                var model = new OnboardingProfileListItem
                {
                    Id = item.Id,
                    DisplayName = BuildProfileDisplayName(item.FirstName, item.LastName, item.FullName),
                    FullName = item.FullName,
                    Username = item.Username,
                    Department = item.Department,
                    JobTitle = item.JobTitle,
                    Supervisor = item.Supervisor,
                    Email = item.Email,
                    EntryDate = item.EntryDate,
                    AssignedAgentDisplayName = ResolveUserDisplayName(item.AssignedDisplayName, item.AssignedFirstName, item.AssignedLastName, item.AssignedUserName, item.AssignedEmail),
                    Status = item.Status,
                    MeasureTotal = item.MeasureTotal,
                    MeasureCompleted = item.MeasureCompleted,
                    ChecklistTotal = item.ChecklistTotal,
                    ChecklistCompleted = item.ChecklistCompleted,
                    LastModifiedAt = item.LastModifiedAt
                };

                model.ProgressPercent = CalculateProgressPercent(model.MeasureCompleted, model.MeasureTotal, model.ChecklistCompleted, model.ChecklistTotal);
                return model;
            })
            .ToList();

        return items;
    }

    public async Task<OnboardingProfile?> GetProfileAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var profile = await context.OnboardingProfiles
            .AsNoTracking()
            .Include(entry => entry.LinkedUser)
            .Include(entry => entry.AssignedAgentUser)
            .Include(entry => entry.Attachment)
                .ThenInclude(attachment => attachment!.UploadedByUser)
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
        NormalizeAndSyncProfile(profile);
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

        NormalizeAndSyncProfile(profile);
        ApplyProfileValues(profile, existing);

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
            Salutation = source.Salutation,
            FirstName = source.FirstName,
            LastName = source.LastName,
            FullName = source.FullName,
            Username = source.Username,
            TicketNumber = source.TicketNumber,
            EntryDate = source.EntryDate,
            ExitDate = source.ExitDate,
            Department = source.Department,
            JobTitle = source.JobTitle,
            Location = source.Location,
            Supervisor = source.Supervisor,
            Email = source.Email,
            Phone = source.Phone,
            PhoneNumber = source.PhoneNumber,
            Mobile = source.Mobile,
            Hostname = source.Hostname,
            DeviceNumber = source.DeviceNumber,
            PrinterCardNumber = source.PrinterCardNumber,
            LinkedUserId = null,
            AssignedAgentUserId = source.AssignedAgentUserId,
            Status = OnboardingProfileStatus.Draft,
            StartDate = null,
            TargetDate = source.TargetDate,
            CompletedAt = null,
            Notes = source.Notes,
            CreatedAt = now,
            LastModifiedAt = now
        };

        ApplyCloneNameSuffix(clone);
        NormalizeAndSyncProfile(clone);

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

    public async Task<List<OnboardingAssigneeLookupItem>> GetAssigneeLookupAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var users = await context.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email
            })
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new OnboardingAssigneeLookupItem
            {
                UserId = user.Id,
                DisplayName = ResolveUserDisplayName(user.DisplayName, user.FirstName, user.LastName, user.UserName, user.Email),
                UserName = user.UserName,
                Email = user.Email
            })
            .OrderBy(user => user.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(user => user.UserName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<OnboardingProfileAttachmentInfo?> GetAttachmentInfoAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.OnboardingProfileAttachments
            .AsNoTracking()
            .Include(item => item.UploadedByUser)
            .FirstOrDefaultAsync(item => item.ProfileId == profileId, cancellationToken);

        return attachment is null ? null : MapAttachmentInfo(attachment);
    }

    public async Task<OnboardingProfileAttachmentData?> GetAttachmentContentAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.OnboardingProfileAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProfileId == profileId, cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        return new OnboardingProfileAttachmentData
        {
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            Content = attachment.Content
        };
    }

    public async Task<OnboardingProfileAttachmentInfo> UploadOrReplaceAttachmentAsync(
        int profileId,
        string originalFileName,
        string contentType,
        byte[] content,
        string? uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.OnboardingProfiles.AnyAsync(profile => profile.Id == profileId, cancellationToken))
        {
            throw new InvalidOperationException($"Onboarding-Profil mit ID {profileId} wurde nicht gefunden.");
        }

        var validatedFileName = NormalizeAndLimitFileName(originalFileName);
        var normalizedContentType = ValidateAndResolveAttachmentContentType(validatedFileName, contentType);

        if (content.Length == 0)
        {
            throw new InvalidOperationException("Die Datei ist leer.");
        }

        if (content.Length > MaxAttachmentSizeBytes)
        {
            throw new InvalidOperationException("Die Datei ist zu groß. Erlaubt sind maximal 15 MB.");
        }

        var now = DateTime.UtcNow;
        var normalizedUploadedByUserId = NormalizeOptional(uploadedByUserId);

        var attachment = await context.OnboardingProfileAttachments
            .FirstOrDefaultAsync(item => item.ProfileId == profileId, cancellationToken);

        if (attachment is null)
        {
            attachment = new OnboardingProfileAttachment
            {
                ProfileId = profileId
            };

            context.OnboardingProfileAttachments.Add(attachment);
        }

        attachment.OriginalFileName = validatedFileName;
        attachment.ContentType = normalizedContentType;
        attachment.Size = content.LongLength;
        attachment.Content = [.. content];
        attachment.UploadedAt = now;
        attachment.UploadedByUserId = normalizedUploadedByUserId;

        await TouchProfileAsync(context, profileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var savedAttachment = await context.OnboardingProfileAttachments
            .AsNoTracking()
            .Include(item => item.UploadedByUser)
            .FirstAsync(item => item.ProfileId == profileId, cancellationToken);

        return MapAttachmentInfo(savedAttachment);
    }

    public async Task<bool> DeleteAttachmentAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.OnboardingProfileAttachments
            .FirstOrDefaultAsync(item => item.ProfileId == profileId, cancellationToken);

        if (attachment is null)
        {
            return false;
        }

        context.OnboardingProfileAttachments.Remove(attachment);
        await TouchProfileAsync(context, profileId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
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

        var normalizedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new InvalidOperationException("Der Maßnahmeneintrag darf nicht leer sein.");
        }

        var entry = new OnboardingMeasureEntry
        {
            ProfileId = profileId,
            CatalogItemId = catalogItemId,
            Value = normalizedValue,
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

    private static void NormalizeAndSyncProfile(OnboardingProfile profile)
    {
        var legacyFullName = NormalizeOptional(profile.FullName);

        profile.Salutation = Enum.IsDefined(profile.Salutation)
            ? profile.Salutation
            : OnboardingSalutation.Unspecified;

        profile.FirstName = NormalizeOptional(profile.FirstName) ?? string.Empty;
        profile.LastName = NormalizeOptional(profile.LastName) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(profile.FirstName) && string.IsNullOrWhiteSpace(profile.LastName) && !string.IsNullOrWhiteSpace(legacyFullName))
        {
            SplitLegacyFullName(legacyFullName, out var firstName, out var lastName);
            profile.FirstName = firstName;
            profile.LastName = lastName;
        }

        profile.Username = NormalizeOptional(profile.Username);
        profile.TicketNumber = NormalizeOptional(profile.TicketNumber);
        profile.Department = NormalizeOptional(profile.Department);
        profile.JobTitle = NormalizeOptional(profile.JobTitle);
        profile.Location = NormalizeOptional(profile.Location);
        profile.Supervisor = NormalizeOptional(profile.Supervisor);
        profile.Email = NormalizeOptional(profile.Email);
        profile.Phone = NormalizeOptional(profile.Phone);
        profile.Mobile = NormalizeOptional(profile.Mobile);
        profile.PhoneNumber = NormalizeOptional(profile.PhoneNumber);
        profile.Hostname = NormalizeOptional(profile.Hostname);
        profile.DeviceNumber = NormalizeOptional(profile.DeviceNumber);
        profile.PrinterCardNumber = NormalizeOptional(profile.PrinterCardNumber);
        profile.LinkedUserId = NormalizeOptional(profile.LinkedUserId);
        profile.AssignedAgentUserId = NormalizeOptional(profile.AssignedAgentUserId);
        profile.Notes = NormalizeOptional(profile.Notes);

        if (string.IsNullOrWhiteSpace(profile.Phone))
        {
            profile.Phone = profile.PhoneNumber;
        }

        profile.PhoneNumber = profile.Phone;
        profile.EntryDate = profile.EntryDate == default ? DateTime.UtcNow.Date : profile.EntryDate.Date;
        profile.ExitDate = profile.ExitDate?.Date;
        profile.StartDate = profile.StartDate?.Date;
        profile.TargetDate = profile.TargetDate?.Date;
        profile.FullName = BuildProfileDisplayName(profile.FirstName, profile.LastName, legacyFullName);
    }

    private static void ApplyProfileValues(OnboardingProfile source, OnboardingProfile target)
    {
        target.Salutation = source.Salutation;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.FullName = source.FullName;
        target.Username = source.Username;
        target.TicketNumber = source.TicketNumber;
        target.EntryDate = source.EntryDate;
        target.ExitDate = source.ExitDate;
        target.Department = source.Department;
        target.JobTitle = source.JobTitle;
        target.Location = source.Location;
        target.Supervisor = source.Supervisor;
        target.Email = source.Email;
        target.Phone = source.Phone;
        target.Mobile = source.Mobile;
        target.PhoneNumber = source.PhoneNumber;
        target.Hostname = source.Hostname;
        target.DeviceNumber = source.DeviceNumber;
        target.PrinterCardNumber = source.PrinterCardNumber;
        target.LinkedUserId = source.LinkedUserId;
        target.AssignedAgentUserId = source.AssignedAgentUserId;
        target.StartDate = source.StartDate;
        target.TargetDate = source.TargetDate;
        target.Notes = source.Notes;
        target.Status = source.Status;
    }

    private static void ApplyCloneNameSuffix(OnboardingProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.LastName))
        {
            profile.LastName = $"{profile.LastName} (Kopie)";
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.FirstName))
        {
            profile.FirstName = $"{profile.FirstName} (Kopie)";
            return;
        }

        profile.FullName = $"{profile.FullName} (Kopie)";
    }

    private static string BuildProfileDisplayName(string? firstName, string? lastName, string? legacyFullName)
    {
        var normalizedFirstName = NormalizeOptional(firstName);
        var normalizedLastName = NormalizeOptional(lastName);

        if (!string.IsNullOrWhiteSpace(normalizedFirstName) || !string.IsNullOrWhiteSpace(normalizedLastName))
        {
            return string.Join(" ", new[] { normalizedFirstName, normalizedLastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return NormalizeOptional(legacyFullName) ?? string.Empty;
    }

    private static void SplitLegacyFullName(string fullName, out string firstName, out string lastName)
    {
        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            firstName = string.Empty;
            lastName = string.Empty;
            return;
        }

        if (parts.Length == 1)
        {
            firstName = parts[0];
            lastName = string.Empty;
            return;
        }

        firstName = parts[0];
        lastName = string.Join(" ", parts.Skip(1));
    }

    private static string ResolveUserDisplayName(string? displayName, string? firstName, string? lastName, string? userName, string? email)
    {
        var normalizedDisplayName = NormalizeOptional(displayName);
        if (!string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            return normalizedDisplayName;
        }

        var combinedName = BuildProfileDisplayName(firstName, lastName, null);
        if (!string.IsNullOrWhiteSpace(combinedName))
        {
            return combinedName;
        }

        return NormalizeOptional(userName)
            ?? NormalizeOptional(email)
            ?? "Unbekannt";
    }

    private static OnboardingProfileAttachmentInfo MapAttachmentInfo(OnboardingProfileAttachment attachment)
    {
        return new OnboardingProfileAttachmentInfo
        {
            ProfileId = attachment.ProfileId,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            Size = attachment.Size,
            UploadedAt = attachment.UploadedAt,
            UploadedByUserId = attachment.UploadedByUserId,
            UploadedByDisplayName = attachment.UploadedByUser is null
                ? null
                : ResolveUserDisplayName(
                    attachment.UploadedByUser.DisplayName,
                    attachment.UploadedByUser.FirstName,
                    attachment.UploadedByUser.LastName,
                    attachment.UploadedByUser.UserName,
                    attachment.UploadedByUser.Email)
        };
    }

    private static string NormalizeAndLimitFileName(string fileName)
    {
        var normalized = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Der Dateiname ist ungültig.");
        }

        const int maxLength = 260;
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var extension = Path.GetExtension(normalized);
        var baseName = Path.GetFileNameWithoutExtension(normalized);
        var maxBaseNameLength = Math.Max(1, maxLength - extension.Length);

        if (baseName.Length > maxBaseNameLength)
        {
            baseName = baseName[..maxBaseNameLength];
        }

        return $"{baseName}{extension}";
    }

    private static string ValidateAndResolveAttachmentContentType(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AttachmentContentTypeByExtension.TryGetValue(extension, out var resolvedContentType))
        {
            throw new InvalidOperationException("Nur PDF-, DOC- oder DOCX-Dateien sind erlaubt.");
        }

        var normalizedContentType = NormalizeOptional(contentType);
        if (!string.IsNullOrWhiteSpace(normalizedContentType))
        {
            var separatorIndex = normalizedContentType.IndexOf(';');
            if (separatorIndex >= 0)
            {
                normalizedContentType = normalizedContentType[..separatorIndex].Trim();
            }

            if (!string.Equals(normalizedContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
                && !AllowedAttachmentContentTypes.Contains(normalizedContentType))
            {
                throw new InvalidOperationException("Der Dateityp wird nicht unterstützt. Erlaubt sind PDF, DOC und DOCX.");
            }
        }

        return resolvedContentType;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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
