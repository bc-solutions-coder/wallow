using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Identity.Infrastructure.Services;

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

        List<FoundryUser> users = await dbContext.Users
            .Where(u => memberUserIds.Contains(u.Id))
            .ToListAsync(ct);

        Dictionary<Guid, FoundryUser> userLookup = users.ToDictionary(u => u.Id);

        List<UserDto> result = new(organization.Members.Count);
        foreach (OrganizationMember member in organization.Members)
        {
            if (userLookup.TryGetValue(member.UserId, out FoundryUser? user))
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

    private async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct)
    {
        FoundryUser? user = await dbContext.Users
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
            .Replace("--", "-")
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
}
