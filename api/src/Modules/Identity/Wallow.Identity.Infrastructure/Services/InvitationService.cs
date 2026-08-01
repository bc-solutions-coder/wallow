using Microsoft.EntityFrameworkCore;
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

public sealed class InvitationService(
    IInvitationRepository invitationRepository,
    IMembershipRepository membershipRepository,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    IdentityDbContext dbContext) : IInvitationService
{
    private const string MemberRoleName = "user";

    public async Task<Invitation> CreateInvitationAsync(Guid tenantId, string email, Guid createdByUserId, CancellationToken ct = default)
    {
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddDays(7);

        Invitation invitation = Invitation.Create(
            TenantId.Create(tenantId),
            email,
            expiresAt,
            createdByUserId,
            timeProvider);

        invitationRepository.Add(invitation);
        await invitationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new InvitationCreatedEvent
        {
            InvitationId = invitation.Id.Value,
            TenantId = invitation.TenantId.Value,
            Email = email,
            Token = invitation.Token,
            ExpiresAt = expiresAt
        });

        return invitation;
    }

    public async Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default)
    {
        InvitationId id = InvitationId.Create(invitationId);
        Invitation invitation = await invitationRepository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException("Invitation", invitationId);

        invitation.Revoke(actorId, timeProvider);
        await invitationRepository.SaveChangesAsync(ct);
    }

    public Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default)
    {
        return invitationRepository.GetByTokenAsync(token, ct);
    }

    /// <summary>
    /// Turns an invitation into membership of the inviting organization. Acceptance is the one join
    /// path that does not consult the organization's enrollment policy — being invited by someone
    /// holding <c>OrganizationsManageMembers</c> IS the authorization.
    /// </summary>
    public async Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default)
    {
        Invitation invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new EntityNotFoundException("Invitation", token);

        await GuardInvitedIdentityAsync(invitation, userId, ct);

        try
        {
            invitation.Accept(userId, timeProvider);
        }
        catch (BusinessRuleException)
        {
            // Accept settles a lapsed invitation to Expired before refusing. Persist that, so a
            // token the sweep has not reached yet stops being resolvable from this attempt on.
            await invitationRepository.SaveChangesAsync(ct);
            throw;
        }

        Guid organizationId = invitation.TenantId.Value;
        Membership? membership = await membershipRepository.GetAsync(userId, organizationId, ct);
        bool joined = await ApplyMembershipAsync(membership, userId, organizationId, ct);

        // The invitation and the membership are tracked on one DbContext, so this is a single
        // transaction. Splitting it either burns the token without granting access or grants
        // access while leaving a live token behind.
        await invitationRepository.SaveChangesAsync(ct);

        if (joined)
        {
            await messageBus.PublishAsync(new OrganizationMemberAddedEvent
            {
                OrganizationId = organizationId,
                TenantId = organizationId,
                UserId = userId,
                Email = invitation.Email
            });
        }
    }

    /// <summary>
    /// Returns whether this acceptance actually added the person to the organization, which is
    /// false when they were already an active member.
    /// </summary>
    private async Task<bool> ApplyMembershipAsync(
        Membership? membership, Guid userId, Guid organizationId, CancellationToken ct)
    {
        if (membership is null)
        {
            Guid roleId = await ResolveRoleIdAsync(MemberRoleName, ct);
            membershipRepository.Add(Membership.Enroll(
                userId, OrganizationId.Create(organizationId), roleId, timeProvider));
            return true;
        }

        switch (membership.Status)
        {
            // An access request the invitation supersedes. Leaving it Pending would strand a row
            // that blocks the next legitimate request and outlives a later denial.
            case MembershipStatus.Pending:
                membership.Approve(await ResolveRoleIdAsync(MemberRoleName, ct), userId, timeProvider);
                return true;

            case MembershipStatus.Active:
                return false;

            // Reinstating someone an admin took access away from is that admin's decision to make
            // explicitly, not something an invitation issued against an email address does quietly.
            default:
                throw new BusinessRuleException(
                    "Identity.MembershipNotReinstatable",
                    $"Membership of this organization is '{membership.Status}' and cannot be resumed by invitation");
        }
    }

    /// <summary>
    /// Binds acceptance to the person the invitation names. Without both halves a leaked or
    /// forwarded token is a join credential for whoever holds it — and in an invite-only
    /// organization that token is the entire perimeter.
    /// </summary>
    private async Task GuardInvitedIdentityAsync(Invitation invitation, Guid userId, CancellationToken ct)
    {
        WallowUser user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new EntityNotFoundException("User", userId);

        if (!user.EmailConfirmed)
        {
            throw new BusinessRuleException(
                "Identity.InvitationEmailNotVerified",
                "Verify your email address before accepting an invitation");
        }

        // Compare normalized: Identity stores NormalizedEmail upper-invariant, while the invitation
        // holds the address exactly as the inviter typed it.
        if (!string.Equals(user.NormalizedEmail, invitation.Email.ToUpperInvariant(), StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Identity.InvitationEmailMismatch",
                "This invitation was issued to a different email address");
        }
    }

    /// <summary>
    /// The role an invited member starts with. Task 4.2 replaces this with the organization's
    /// configured default.
    /// </summary>
    private async Task<Guid> ResolveRoleIdAsync(string roleName, CancellationToken ct)
    {
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

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        // The sweep runs from a background job, where no tenant is resolved and the filter would
        // therefore match nothing. It is deliberately every tenant's expired invitations.
        List<Invitation> expiredInvitations = await dbContext.Invitations
            .AsTracking()
            .IgnoreQueryFilters()
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (Invitation invitation in expiredInvitations)
        {
            invitation.MarkExpired();
        }

        if (expiredInvitations.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
