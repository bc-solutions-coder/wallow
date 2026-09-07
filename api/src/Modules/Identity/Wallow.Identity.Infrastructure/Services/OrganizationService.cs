using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OrganizationService(
    IOrganizationRepository organizationRepository,
    IMembershipRepository membershipRepository,
    IdentityDbContext dbContext,
    IAccessRevoker accessRevoker,
    IOrganizationAdminEmailResolver adminEmails,
    ILastOwnerGuard lastOwnerGuard,
    IRegisteredClientRepository registeredClients,
    IOpenIddictApplicationManager applicationManager,
    IMessageBus messageBus,
    IDbContextOutbox outbox,
    TimeProvider timeProvider,
    ILogger<OrganizationService> logger) : IOrganizationService
{
    /// <summary>
    /// Admin role granted to organization creators and bootstrap owners.
    /// </summary>
    private const string AdminRoleName = "admin";

    public async Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, Guid? creatorUserId = null, CancellationToken ct = default)
    {
        LogCreatingOrganization(name);

        string slug = GenerateSlug(name);
        // System creation supplies no creator: no owner is enrolled and audit attribution is empty.
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

        // Settings belong to the new organization's tenant, not the caller's tenant.
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
            // Record the creator's owner grant with the creator as actor.
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
            // Grant records the admin actor after Enroll initially attributes creation to the member.
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

        // Bootstrap has no separate actor; attribute ownership to the new owner.
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
            throw new BusinessRuleException(IdentityErrors.MemberNotFound);
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

        // Commit archive state and database revocations together. Realtime disconnection
        // cannot roll back; reactivation does not restore revoked credentials or client status.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await organizationRepository.SaveChangesAsync(token);
                await accessRevoker.RevokeOrganizationAsync(organizationId, token);
                await transaction.CommitAsync(token);
            });

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

    public async Task SuspendByPlatformAsync(Guid organizationId, string reason, Guid actorId, CancellationToken ct = default)
    {
        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        organization.SuspendByPlatform(reason, actorId, timeProvider);

        // Commit platform suspension and database token revocations together.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await organizationRepository.SaveChangesAsync(token);
                await accessRevoker.RevokeOrganizationAsync(organizationId, token);
                await transaction.CommitAsync(token);
            });

        IReadOnlyList<string> recipients = await adminEmails.ResolveAsync(organizationId, ct);

        await messageBus.PublishAsync(new OrganizationSuspendedByPlatformEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            OrganizationName = organization.Name,
            ActorId = actorId,
            Reason = organization.PlatformSuspensionReason ?? reason,
            RecipientEmails = recipients
        });

        LogOrganizationSuspendedByPlatform(organizationId, actorId);
    }

    public async Task ReinstateByPlatformAsync(Guid organizationId, Guid actorId, CancellationToken ct = default)
    {
        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        organization.ReinstateByPlatform(actorId, timeProvider);
        await organizationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationReinstatedByPlatformEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            ActorId = actorId
        });

        LogOrganizationReinstatedByPlatform(organizationId, actorId);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        string confirmedName,
        Guid actorId,
        bool byPlatformOperator,
        CancellationToken ct = default)
    {
        LogDeletingOrganization(organizationId);

        OrganizationId id = OrganizationId.Create(organizationId);
        Organization? organization = await organizationRepository.GetByIdAsync(id, ct);

        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found");
        }

        organization.EnsureDeletable(byPlatformOperator);
        Organization.ConfirmNameForDeletion(organization, confirmedName);

        string orgName = organization.Name;
        TenantId tenantId = TenantId.Create(organizationId);

        // Capture recipients before deleting the memberships used to resolve them.
        IReadOnlyList<string> recipients = await adminEmails.ResolveAsync(organizationId, ct);

        // Commit Identity deletions and the outbox event together; flush only after commit.
        // Revocation can disconnect realtime clients, an external effect that cannot roll back.
        // Clearing tracked token graphs requires the remaining deletes to execute directly.
        // Execution-strategy retries may redeliver the event; consumers must be idempotent.
        outbox.Enroll(dbContext);
        bool deleted = false;
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);

                await TelemetryOwnership.LockOrganizationAsync(dbContext, organizationId, token);

                // Snapshot clients inside the transaction, after the preliminary checks.
                IReadOnlyList<RegisteredClient> boundClients =
                    await registeredClients.ListByOrganizationAsync(organizationId, token);
                List<string> clientIds = [.. boundClients.Select(c => c.ClientId)];

                await accessRevoker.RevokeOrganizationAsync(organizationId, token);

                foreach (string clientId in clientIds)
                {
                    object? application = await applicationManager.FindByClientIdAsync(clientId, token);
                    if (application is not null)
                    {
                        RevokedTokenDetacher.DetachRevokedTokens(dbContext, application);
                        await applicationManager.DeleteAsync(application, token);
                    }
                }

                if (clientIds.Count > 0)
                {
                    await dbContext.SsoSessionClients
                        .Where(s => clientIds.Contains(s.ClientId))
                        .ExecuteDeleteAsync(token);
                }

                List<TelemetryRegistration> telemetry = await dbContext.TelemetryRegistrations.AsTracking()
                    .Where(e => e.OrganizationId == organizationId).ToListAsync(token);
                foreach (TelemetryRegistration registration in telemetry) { registration.Revoke(deleted: true, timeProvider); }
                await dbContext.SaveChangesAsync(token);

                await dbContext.RegisteredClients
                    .Where(c => c.OrganizationId == organizationId)
                    .ExecuteDeleteAsync(token);

                // Membership has no organization FK, so delete its rows explicitly.
                await dbContext.Memberships
                    .Where(m => m.OrganizationId == id)
                    .ExecuteDeleteAsync(token);

                await dbContext.Invitations
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId)
                    .ExecuteDeleteAsync(token);

                await dbContext.ActiveSessions
                    .Where(session => session.TenantId == organizationId)
                    .ExecuteDeleteAsync(token);

                await dbContext.OrganizationSettings
                    .IgnoreQueryFilters()
                    .Where(settings => settings.OrganizationId == id)
                    .ExecuteDeleteAsync(token);

                await dbContext.OrganizationBrandings
                    .IgnoreQueryFilters()
                    .Where(branding => branding.OrganizationId == id)
                    .ExecuteDeleteAsync(token);

                await dbContext.TenantSettings
                    .IgnoreQueryFilters()
                    .Where(setting => setting.TenantId == tenantId)
                    .ExecuteDeleteAsync(token);

                await dbContext.UserSettings
                    .IgnoreQueryFilters()
                    .Where(setting => setting.TenantId == tenantId)
                    .ExecuteDeleteAsync(token);

                int organizationRows = await dbContext.Organizations
                    .IgnoreQueryFilters()
                    .Where(o => o.Id == id)
                    .ExecuteDeleteAsync(token);

                // Publish only when this attempt deleted the organization row.
                if (organizationRows > 0)
                {
                    await outbox.PublishAsync(new OrganizationDeletedEvent
                    {
                        OrganizationId = organizationId,
                        TenantId = organizationId,
                        OrganizationName = orgName,
                        ActorId = actorId,
                        RecipientEmails = recipients
                    });
                    deleted = true;
                }

                await transaction.CommitAsync(token);
            });

        if (!deleted)
        {
            LogOrganizationAlreadyDeleted(organizationId);
            return;
        }

        await outbox.FlushOutgoingMessagesAsync();

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
    /// Updates enrollment settings, creating the settings row when absent.
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
    /// Rejects a nonexistent default role at configuration time.
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
            throw new BusinessRuleException(IdentityErrors.RoleNotFound, "The requested default role does not exist");
        }
    }

    /// <summary>
    /// Reads by organization across tenant filters to find the globally unique settings row.
    /// Tracks it because callers mutate it and the context defaults to no tracking.
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

        // Give active members without MFA a grace deadline used by the login policy.
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
        // Track changes despite the context's no-tracking default.
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
        // TODO: Upload through the Storage module (for example, a Wolverine upload request)
        // and return the stored URL. This placeholder returns a path without persisting bytes.
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
    /// Resolves a global role by normalized name; missing roles are caller errors.
    /// </summary>
    private async Task<Guid> ResolveRoleIdAsync(string roleName, CancellationToken ct)
    {
        // Match the stored normalized role name directly.
        string normalizedName = roleName.ToUpperInvariant();

        WallowRole? role = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

        if (role is null)
        {
            throw new BusinessRuleException(IdentityErrors.RoleNotFound, $"Role '{roleName}' does not exist");
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
                memberCounts.TryGetValue(o.Id.Value, out int count) ? count : 0,
                o.PlatformSuspendedAt,
                o.PlatformSuspensionReason))
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrgId} was already deleted by a concurrent request; skipping the deleted event")]
    private partial void LogOrganizationAlreadyDeleted(Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Organization {OrgId} suspended by platform actor {ActorId}")]
    private partial void LogOrganizationSuspendedByPlatform(Guid orgId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrgId} reinstated by platform actor {ActorId}")]
    private partial void LogOrganizationReinstatedByPlatform(Guid orgId, Guid actorId);
}
