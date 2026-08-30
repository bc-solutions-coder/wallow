using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OrganizationService(
    IOrganizationRepository organizationRepository,
    IMembershipRepository membershipRepository,
    IdentityDbContext dbContext,
    IAccessRevoker accessRevoker,
    ILastOwnerGuard lastOwnerGuard,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    ILogger<OrganizationService> logger) : IOrganizationService
{
    /// <summary>
    /// The role a creator is granted in the organization they create, and the role every
    /// membership-carrying seed path starts from.
    /// </summary>
    private const string AdminRoleName = "admin";

    public async Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, Guid? creatorUserId = null, CancellationToken ct = default)
    {
        LogCreatingOrganization(name);

        string slug = GenerateSlug(name);
        // System-initiated creation (SCIM sync, pre-registered client provisioning) passes no creator;
        // audit fields fall back to Guid.Empty and no member is added.
        Guid createdByUserId = creatorUserId ?? Guid.Empty;

        Organization organization = Organization.Create(
            default,
            name,
            slug,
            createdByUserId,
            timeProvider);

        if (creatorUserId.HasValue)
        {
            Guid adminRoleId = await ResolveRoleIdAsync(AdminRoleName, ct);

            Membership ownerMembership = Membership.Enroll(
                creatorUserId.Value, organization.Id, adminRoleId, timeProvider);
            ownerMembership.MarkOwner(true, creatorUserId.Value, timeProvider);

            membershipRepository.Add(ownerMembership);
        }

        organizationRepository.Add(organization);
        await organizationRepository.SaveChangesAsync(ct);
        await membershipRepository.SaveChangesAsync(ct);

        // The settings belong to the new organization, which is its own tenant. Reading the
        // caller's ambient tenant here stamped them onto whoever happened to be creating it.
        OrganizationSettings defaultSettings = OrganizationSettings.Create(
            organization.Id,
            organization.TenantId,
            requireMfa: false,
            allowPasswordlessLogin: true,
            mfaGracePeriodDays: 7,
            createdByUserId,
            timeProvider);

        dbContext.OrganizationSettings.Add(defaultSettings);
        await dbContext.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationCreatedEvent
        {
            OrganizationId = organization.Id.Value,
            TenantId = organization.TenantId.Value,
            Name = name,
            Domain = domain,
            CreatorEmail = creatorEmail ?? string.Empty
        });

        if (creatorUserId.HasValue)
        {
            // Creating the organization is the only way anyone becomes its owner, so it is the
            // only place the grant can be recorded.
            await PublishTransitionAsync(
                MembershipTransition.OwnerMarked,
                organization.Id.Value,
                creatorUserId.Value,
                creatorUserId.Value);

            LogCreatorAddedAsAdmin(creatorUserId.Value, organization.Id.Value);
        }

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

        return await MapToDtoAsync(organization, ct);
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(
        string? search = null,
        int first = 0,
        int max = 20,
        CancellationToken ct = default)
    {
        List<Organization> organizations = await organizationRepository.GetAllAsync(search, first, max, ct);
        return await MapToDtosAsync(organizations, ct);
    }

    public async Task AddMemberAsync(Guid orgId, Guid userId, string roleName, Guid actorId, CancellationToken ct = default)
    {
        LogAddingMember(userId, orgId);

        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {orgId} not found");
        }

        Guid roleId = await ResolveRoleIdAsync(roleName, ct);
        Membership? membership = await membershipRepository.GetAsync(userId, orgId, ct);

        if (membership is null)
        {
            // Enroll models someone joining under their own steam and stamps no actor. An admin
            // adding a member is a different act, so grant on top of it to record who did it.
            Membership added = Membership.Enroll(userId, id, roleId, timeProvider);
            added.Grant(roleId, actorId, timeProvider);
            membershipRepository.Add(added);
        }
        else
        {
            membership.Grant(roleId, actorId, timeProvider);
        }

        await membershipRepository.SaveChangesAsync(ct);

        string email = await GetUserEmailAsync(userId, ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = orgId,
            TenantId = orgId,
            UserId = userId,
            Email = email
        });

        await PublishTransitionAsync(MembershipTransition.Added, orgId, userId, actorId);

        LogMemberAdded(userId, orgId);
    }

    public async Task EnrollOwnerAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogEnrollingOwner(userId, orgId);

        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {orgId} not found");
        }

        Guid adminRoleId = await ResolveRoleIdAsync(AdminRoleName, ct);
        Membership? membership = await membershipRepository.GetAsync(userId, orgId, ct);

        if (membership is null)
        {
            membership = Membership.Enroll(userId, id, adminRoleId, timeProvider);
            membershipRepository.Add(membership);
        }
        else
        {
            membership.Grant(adminRoleId, userId, timeProvider);
        }

        // The same self-attribution as creating the organization: there is no other actor at
        // bootstrap, and a blank one would read as an unattributed grant.
        membership.MarkOwner(true, userId, timeProvider);
        await membershipRepository.SaveChangesAsync(ct);

        string email = await GetUserEmailAsync(userId, ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = orgId,
            TenantId = orgId,
            UserId = userId,
            Email = email
        });

        await PublishTransitionAsync(MembershipTransition.OwnerMarked, orgId, userId, userId);

        LogOwnerEnrolled(userId, orgId);
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        LogRemovingMember(userId, orgId);

        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {orgId} not found");
        }

        string email = await GetUserEmailAsync(userId, ct);

        Membership? membership = await membershipRepository.GetAsync(userId, orgId, ct);

        if (membership is null)
        {
            throw new BusinessRuleException(
                "Identity.MemberNotFound",
                "User is not a member of this organization");
        }

        await lastOwnerGuard.ExecuteDepartureAsync(orgId, userId, async token =>
        {
            membershipRepository.Remove(membership);
            await membershipRepository.SaveChangesAsync(token);
        }, ct);

        await accessRevoker.RevokeMembershipAsync(userId, orgId, ct);

        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent
        {
            OrganizationId = orgId,
            TenantId = orgId,
            UserId = userId,
            Email = email
        });

        await PublishTransitionAsync(MembershipTransition.Removed, orgId, userId, actorId);

        LogMemberRemoved(userId, orgId);
    }

    private ValueTask PublishTransitionAsync(
        MembershipTransition transition, Guid organizationId, Guid userId, Guid actorId) =>
        messageBus.PublishAsync(new MembershipTransitionedEvent
        {
            Transition = transition,
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            ActorId = actorId,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime
        });

    public async Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        OrganizationId id = OrganizationId.Create(orgId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            return [];
        }

        IReadOnlyList<Membership> memberships = await membershipRepository.GetForOrganizationAsync(
            orgId, MembershipStatus.Active, ct);

        List<Guid> memberUserIds = [.. memberships.Select(m => m.UserId)];
        if (memberUserIds.Count == 0)
        {
            return [];
        }

        List<WallowUser> users = await dbContext.Users
            .Where(u => memberUserIds.Contains(u.Id))
            .ToListAsync(ct);

        Dictionary<Guid, WallowUser> userLookup = users.ToDictionary(u => u.Id);
        Dictionary<Guid, string> roleNames = await GetRoleNameLookupAsync(
            [.. memberships.SelectMany(m => m.RoleIds).Distinct()], ct);

        List<UserDto> result = new(memberships.Count);
        foreach (Membership membership in memberships)
        {
            if (userLookup.TryGetValue(membership.UserId, out WallowUser? user))
            {
                result.Add(new UserDto(
                    user.Id,
                    user.Email ?? string.Empty,
                    user.FirstName,
                    user.LastName,
                    user.IsActive,
                    [.. membership.RoleIds
                        .Where(roleNames.ContainsKey)
                        .Select(roleId => roleNames[roleId])
                        .Order(StringComparer.Ordinal)]));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        List<Organization> organizations = await organizationRepository.GetByUserIdAsync(userId, ct);
        return await MapToDtosAsync(organizations, ct);
    }

    public async Task<IReadOnlyList<MyOrganizationDto>> GetMyOrganizationsAsync(
        Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<Membership> memberships = await membershipRepository.GetForUserAsync(userId, ct);
        Dictionary<Guid, Membership> active = memberships
            .Where(m => m.Status == MembershipStatus.Active)
            .ToDictionary(m => m.OrganizationId.Value);

        if (active.Count == 0)
        {
            return [];
        }

        List<Organization> organizations = await organizationRepository.GetByUserIdAsync(userId, ct);

        return
        [
            .. organizations
                .Where(o => o.IsActive && active.ContainsKey(o.Id.Value))
                .Select(o => new MyOrganizationDto(
                    o.Id.Value,
                    o.Name,
                    o.Slug,
                    active[o.Id.Value].IsOwner))
        ];
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
            TenantId = organizationId,
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
            TenantId = organizationId,
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

        // Membership carries no foreign key to Organization (OrganizationId is the scope, not a
        // navigation), so nothing cascades — the rows have to go explicitly or they outlive the org.
        IReadOnlyList<Membership> memberships = await membershipRepository.GetForOrganizationAsync(
            organizationId, null, ct);

        foreach (Membership membership in memberships)
        {
            membershipRepository.Remove(membership);
        }

        dbContext.Organizations.Remove(organization);
        await dbContext.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationDeletedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
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
            settings.MfaGracePeriodDays,
            settings.EnrollmentPolicy,
            settings.AccessRequestEmail,
            settings.DefaultRoleId);
    }

    /// <summary>
    /// Changes who may join this organization. A settings row is created on demand so that an
    /// organization which has never had its settings touched can still be opened up.
    /// </summary>
    public async Task UpdateEnrollmentAsync(
        Guid organizationId,
        EnrollmentPolicy enrollmentPolicy,
        string? accessRequestEmail,
        Guid? defaultRoleId,
        Guid actorId,
        CancellationToken ct = default)
    {
        await GuardRoleExistsAsync(defaultRoleId, ct);

        OrganizationSettings settings = await GetOrCreateSettingsAsync(organizationId, actorId, ct);
        settings.UpdateEnrollment(enrollmentPolicy, accessRequestEmail, defaultRoleId, actorId, timeProvider);

        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A default role that does not exist is a policy that admits nobody: every join under it fails
    /// at the moment of enrollment, far from the setting that caused it.
    /// </summary>
    private async Task GuardRoleExistsAsync(Guid? roleId, CancellationToken ct)
    {
        if (roleId is null)
        {
            return;
        }

        bool exists = await dbContext.Roles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Id == roleId.Value, ct);

        if (!exists)
        {
            throw new BusinessRuleException(
                "Identity.RoleNotFound",
                "The requested default role does not exist");
        }
    }

    /// <summary>
    /// The unique constraint on organization_id is global, so the lookup has to see rows the tenant
    /// filter would hide or the insert races it. AsTracking because the DbContext defaults to
    /// NoTracking and mutations would otherwise go unnoticed.
    /// </summary>
    private async Task<OrganizationSettings> GetOrCreateSettingsAsync(
        Guid organizationId, Guid actorId, CancellationToken ct)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        OrganizationSettings? settings = await dbContext.OrganizationSettings
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

        if (settings is not null)
        {
            return settings;
        }

        settings = OrganizationSettings.Create(
            orgId,
            TenantId.Create(organizationId),
            requireMfa: false,
            allowPasswordlessLogin: false,
            mfaGracePeriodDays: 0,
            actorId,
            timeProvider);

        dbContext.OrganizationSettings.Add(settings);

        return settings;
    }

    public async Task UpdateSettingsAsync(Guid organizationId, bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid actorId, CancellationToken ct = default)
    {
        OrganizationSettings settings = await GetOrCreateSettingsAsync(organizationId, actorId, ct);
        settings.Update(requireMfa, allowPasswordlessLogin, mfaGracePeriodDays, actorId, timeProvider);

        await dbContext.SaveChangesAsync(ct);

        // When enabling MFA with a grace period, set MfaGraceDeadline on unenrolled members
        // so the login flow can detect they're within the grace window
        if (requireMfa && mfaGracePeriodDays > 0)
        {
            DateTimeOffset graceDeadline = timeProvider.GetUtcNow().AddDays(mfaGracePeriodDays);

            IReadOnlyList<Membership> memberships = await membershipRepository.GetForOrganizationAsync(
                organizationId, MembershipStatus.Active, ct);

            List<Guid> memberUserIds = [.. memberships.Select(m => m.UserId)];

            List<WallowUser> unenrolledMembers = await dbContext.Users
                .AsTracking()
                .IgnoreQueryFilters()
                .Where(u => memberUserIds.Contains(u.Id) && !u.MfaEnabled)
                .ToListAsync(ct);

            foreach (WallowUser member in unenrolledMembers)
            {
                member.SetMfaGraceDeadline(graceDeadline);
            }

            if (unenrolledMembers.Count > 0)
            {
                await dbContext.SaveChangesAsync(ct);
            }
        }

        await messageBus.PublishAsync(new OrganizationSettingsUpdatedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
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
        // AsTracking ensures EF Core detects mutations even though the DbContext defaults to NoTracking.
        OrganizationBranding? branding = await dbContext.OrganizationBrandings
            .AsTracking()
            .FirstOrDefaultAsync(b => b.OrganizationId == orgId, ct);

        if (branding is null)
        {
            branding = OrganizationBranding.Create(
                orgId,
                TenantId.Create(organizationId),
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
        // TODO: Wire to Storage module via Wolverine integration event (e.g. UploadFileCommand).
        // Should publish a file upload request to the Storage module and return the resulting URL.
        // Tracked placeholder — currently returns a deterministic path without persisting the file.
        string logoPath = $"/storage/organizations/{organizationId}/branding/logo/{fileName}";
        return Task.FromResult(logoPath);
    }

    private async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct)
    {
        WallowUser? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.Email ?? string.Empty;
    }

    /// <summary>
    /// Resolves one role name to its id. Roles are a global catalog addressed by name everywhere
    /// authorization is expressed (<c>RolePermissionMapping</c>), so a name that is not in the
    /// catalog is a caller error rather than an empty grant.
    /// </summary>
    private async Task<Guid> ResolveRoleIdAsync(string roleName, CancellationToken ct)
    {
        // Identity's default normalizer upper-cases invariantly, so this matches what
        // RoleManager wrote without paying for a case-insensitive collation scan.
        string normalizedName = roleName.ToUpperInvariant();

        WallowRole? role = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

        if (role is null)
        {
            throw new BusinessRuleException(
                "Identity.RoleNotFound",
                $"Role '{roleName}' does not exist");
        }

        return role.Id;
    }

    private async Task<Dictionary<Guid, string>> GetRoleNameLookupAsync(
        List<Guid> roleIds,
        CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        List<WallowRole> roles = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .ToListAsync(ct);

        return roles.ToDictionary(r => r.Id, r => r.Name!);
    }

    private async Task<OrganizationDto> MapToDtoAsync(Organization organization, CancellationToken ct)
    {
        IReadOnlyList<OrganizationDto> mapped = await MapToDtosAsync([organization], ct);
        return mapped[0];
    }

    private async Task<IReadOnlyList<OrganizationDto>> MapToDtosAsync(
        List<Organization> organizations,
        CancellationToken ct)
    {
        if (organizations.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<Guid, int> memberCounts = await membershipRepository
            .CountActiveByOrganizationAsync([.. organizations.Select(o => o.Id.Value)], ct);

        return
        [
            .. organizations.Select(o => new OrganizationDto(
                o.Id.Value,
                o.Name,
                null,
                memberCounts.TryGetValue(o.Id.Value, out int count) ? count : 0))
        ];
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Creator {UserId} added as admin member of organization {OrgId}")]
    private partial void LogCreatorAddedAsAdmin(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Organization {OrgId} not found")]
    private partial void LogOrganizationNotFound(Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Adding user {UserId} to organization {OrgId}")]
    private partial void LogAddingMember(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} added to organization {OrgId}")]
    private partial void LogMemberAdded(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrolling user {UserId} as owner of existing organization {OrganizationId}")]
    private partial void LogEnrollingOwner(Guid userId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrolled user {UserId} as owner of organization {OrganizationId}")]
    private partial void LogOwnerEnrolled(Guid userId, Guid organizationId);

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
