using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OrganizationService(
    IOrganizationRepository organizationRepository,
    IdentityDbContext dbContext,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<OrganizationService> logger) : IOrganizationService
{
    public async Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, CancellationToken ct = default)
    {
        LogCreatingOrganization(name);

        string slug = GenerateSlug(name);
        Guid creatorUserId = Guid.Empty; // No authenticated user context available here

        Organization organization = Organization.Create(
            tenantContext.TenantId,
            name,
            slug,
            creatorUserId,
            timeProvider);

        organizationRepository.Add(organization);
        await organizationRepository.SaveChangesAsync(ct);

        OrganizationSettings defaultSettings = OrganizationSettings.Create(
            organization.Id,
            tenantContext.TenantId,
            requireMfa: false,
            allowPasswordlessLogin: true,
            mfaGracePeriodDays: 7,
            creatorUserId,
            timeProvider);

        dbContext.OrganizationSettings.Add(defaultSettings);
        await dbContext.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationCreatedEvent
        {
            OrganizationId = organization.Id.Value,
            TenantId = tenantContext.TenantId.Value,
            Name = name,
            Domain = domain,
            CreatorEmail = creatorEmail ?? string.Empty
        });

        LogOrganizationCreated(name, organization.Id.Value);

        return organization.Id.Value;
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default)
    {
        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            LogOrganizationNotFound(orgId);
            return null;
        }

        return MapToDto(organization);
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(
        string? search = null,
        int first = 0,
        int max = 20,
        CancellationToken ct = default)
    {
        List<Organization> organizations = await organizationRepository.GetAllAsync(search, first, max, ct);
        return organizations.Select(MapToDto).ToList();
    }

    public async Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogAddingMember(userId, orgId);

        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {orgId} not found");
        }

        organization.AddMember(userId, "member", Guid.Empty, timeProvider);
        await organizationRepository.SaveChangesAsync(ct);

        string email = await GetUserEmailAsync(userId, ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = orgId,
            TenantId = tenantContext.TenantId.Value,
            UserId = userId,
            Email = email
        });

        LogMemberAdded(userId, orgId);
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogRemovingMember(userId, orgId);

        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {orgId} not found");
        }

        string email = await GetUserEmailAsync(userId, ct);

        organization.RemoveMember(userId, Guid.Empty, timeProvider);
        await organizationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent
        {
            OrganizationId = orgId,
            TenantId = tenantContext.TenantId.Value,
            UserId = userId,
            Email = email
        });

        LogMemberRemoved(userId, orgId);
    }

    public async Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            return [];
        }

        List<Guid> memberUserIds = organization.Members.Select(m => m.UserId).ToList();
        if (memberUserIds.Count == 0)
        {
            return [];
        }

        List<WallowUser> users = await dbContext.Users
            .Where(u => memberUserIds.Contains(u.Id))
            .ToListAsync(ct);

        Dictionary<Guid, WallowUser> userLookup = users.ToDictionary(u => u.Id);

        List<UserDto> result = new(organization.Members.Count);
        foreach (OrganizationMember member in organization.Members)
        {
            if (userLookup.TryGetValue(member.UserId, out WallowUser? user))
            {
                result.Add(new UserDto(
                    user.Id,
                    user.Email ?? string.Empty,
                    user.FirstName,
                    user.LastName,
                    user.IsActive,
                    [member.Role]));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        List<Organization> organizations = await organizationRepository.GetByUserIdAsync(userId, ct);
        return organizations.Select(MapToDto).ToList();
    }

    public async Task ArchiveAsync(Guid organizationId, Guid actorId, CancellationToken ct = default)
    {
        LogArchivingOrganization(organizationId);

        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        organization.Archive(actorId, timeProvider);
        await organizationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationArchivedEvent
        {
            OrganizationId = organizationId,
            TenantId = tenantContext.TenantId.Value,
            ArchivedBy = actorId
        });

        LogOrganizationArchived(organizationId);
    }

    public async Task ReactivateAsync(Guid organizationId, Guid actorId, CancellationToken ct = default)
    {
        LogReactivatingOrganization(organizationId);

        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        organization.Reactivate(actorId, timeProvider);
        await organizationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationReactivatedEvent
        {
            OrganizationId = organizationId,
            TenantId = tenantContext.TenantId.Value,
            ReactivatedBy = actorId
        });

        LogOrganizationReactivated(organizationId);
    }

    public async Task DeleteAsync(Guid organizationId, string confirmedName, CancellationToken ct = default)
    {
        LogDeletingOrganization(organizationId);

        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        Organization.ConfirmNameForDeletion(organization, confirmedName);

        string orgName = organization.Name;
        dbContext.Organizations.Remove(organization);
        await dbContext.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationDeletedEvent
        {
            OrganizationId = organizationId,
            TenantId = tenantContext.TenantId.Value,
            Name = orgName
        });

        LogOrganizationDeleted(organizationId);
    }

    public async Task<OrganizationSettingsDto?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);
        OrganizationSettings? settings = await dbContext.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

        if (settings is null)
        {
            return null;
        }

        return new OrganizationSettingsDto(
            organizationId,
            settings.RequireMfa,
            settings.AllowPasswordlessLogin,
            settings.MfaGracePeriodDays);
    }

    public async Task UpdateSettingsAsync(Guid organizationId, bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid actorId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);
        OrganizationSettings? settings = await dbContext.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

        if (settings is null)
        {
            throw new InvalidOperationException($"Organization settings for {organizationId} not found");
        }

        settings.Update(requireMfa, allowPasswordlessLogin, mfaGracePeriodDays, actorId, timeProvider);
        await dbContext.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationSettingsUpdatedEvent
        {
            OrganizationId = organizationId,
            TenantId = tenantContext.TenantId.Value,
            RequireMfa = requireMfa,
            AllowPasswordlessLogin = allowPasswordlessLogin,
            MfaGracePeriodDays = mfaGracePeriodDays
        });
    }

    public async Task<OrganizationBrandingDto?> GetBrandingAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);
        OrganizationBranding? branding = await dbContext.OrganizationBrandings
            .FirstOrDefaultAsync(b => b.OrganizationId == orgId, ct);

        if (branding is null)
        {
            return null;
        }

        return new OrganizationBrandingDto(
            organizationId,
            null,
            branding.LogoUrl,
            branding.PrimaryColor,
            branding.AccentColor);
    }

    public async Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, string? displayName, string? logoUrl, string? primaryColor, Guid actorId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);
        OrganizationBranding? branding = await dbContext.OrganizationBrandings
            .FirstOrDefaultAsync(b => b.OrganizationId == orgId, ct);

        if (branding is null)
        {
            branding = OrganizationBranding.Create(
                orgId,
                tenantContext.TenantId,
                logoUrl,
                primaryColor,
                null,
                actorId,
                timeProvider);
            dbContext.OrganizationBrandings.Add(branding);
        }
        else
        {
            branding.Update(logoUrl, primaryColor, branding.AccentColor, actorId, timeProvider);
        }

        await dbContext.SaveChangesAsync(ct);

        return new OrganizationBrandingDto(
            organizationId,
            displayName,
            branding.LogoUrl,
            branding.PrimaryColor,
            branding.AccentColor);
    }

    public Task<string> UploadBrandingLogoAsync(Guid organizationId, Stream logoStream, string fileName, string contentType, Guid actorId, CancellationToken ct = default)
    {
        // Logo upload will be wired to the Storage module via integration events in a future iteration.
        // For now, return a placeholder path based on org ID.
        string logoPath = $"/storage/organizations/{organizationId}/branding/logo/{fileName}";
        return Task.FromResult(logoPath);
    }

    private async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct)
    {
        WallowUser? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.Email ?? string.Empty;
    }

    private static OrganizationDto MapToDto(Organization organization)
    {
        return new OrganizationDto(
            organization.Id.Value,
            organization.Name,
            null,
            organization.Members.Count);
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("--", "-", StringComparison.Ordinal)
            .Trim('-');
    }
}

public sealed partial class OrganizationService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating organization {Name}")]
    private partial void LogCreatingOrganization(string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {Name} created with ID {OrgId}")]
    private partial void LogOrganizationCreated(string name, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Organization {OrgId} not found")]
    private partial void LogOrganizationNotFound(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Adding user {UserId} to organization {OrgId}")]
    private partial void LogAddingMember(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} added to organization {OrgId}")]
    private partial void LogMemberAdded(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing user {UserId} from organization {OrgId}")]
    private partial void LogRemovingMember(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} removed from organization {OrgId}")]
    private partial void LogMemberRemoved(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Archiving organization {OrgId}")]
    private partial void LogArchivingOrganization(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrgId} archived")]
    private partial void LogOrganizationArchived(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reactivating organization {OrgId}")]
    private partial void LogReactivatingOrganization(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrgId} reactivated")]
    private partial void LogOrganizationReactivated(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting organization {OrgId}")]
    private partial void LogDeletingOrganization(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrgId} deleted")]
    private partial void LogOrganizationDeleted(Guid orgId);
}
