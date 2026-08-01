using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class InvitationService(
    IInvitationRepository invitationRepository,
    IMembershipRepository membershipRepository,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    ITenantContext tenantContext,
    IDefaultMemberRoleResolver defaultRoleResolver,
    IdentityDbContext dbContext) : IInvitationService
{

    /// <summary>
    /// Invites an address into the caller's organization, or re-sends the invitation already
    /// outstanding for it. Re-inviting deliberately refreshes ONE token rather than minting a
    /// second: <c>Revoke</c> acts on a single invitation by id, so every extra token is one the
    /// admin cannot see in the list and cannot take back.
    /// </summary>
    public async Task<Invitation> CreateInvitationAsync(string email, Guid createdByUserId, CancellationToken ct = default)
    {
        Guid organizationId = tenantContext.TenantId.Value;
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddDays(7);

        await GuardNotAlreadyAMemberAsync(organizationId, email, ct);

        Invitation? outstanding = await invitationRepository.GetPendingByEmailAsync(organizationId, email, ct);
        Invitation invitation = outstanding ?? NewInvitation(email, expiresAt, createdByUserId);

        if (outstanding is not null)
        {
            outstanding.Renew(expiresAt, createdByUserId, timeProvider);
        }
        else
        {
            invitationRepository.Add(invitation);
        }

        await invitationRepository.SaveChangesAsync(ct);

        // Published either way: clicking invite again usually means the first mail went astray,
        // and the same token re-sent is the same live link.
        await messageBus.PublishAsync(new InvitationCreatedEvent
        {
            InvitationId = invitation.Id.Value,
            TenantId = invitation.TenantId.Value,
            Email = invitation.Email,
            Token = invitation.Token,
            ExpiresAt = invitation.ExpiresAt
        });

        return invitation;
    }

    /// <summary>
    /// The save interceptor stamps TenantId from the ambient tenant regardless of what the entity
    /// carries, so the ambient tenant is the only tenant an invitation can land in.
    /// </summary>
    private Invitation NewInvitation(string email, DateTimeOffset expiresAt, Guid createdByUserId)
    {
        return Invitation.Create(
            tenantContext.TenantId,
            email,
            expiresAt,
            createdByUserId,
            timeProvider);
    }

    /// <summary>
    /// Refuses to invite someone who is already in. An invitation to a sitting member is at best
    /// noise and at worst a live token for an address that no longer needs one.
    /// </summary>
    private async Task GuardNotAlreadyAMemberAsync(Guid organizationId, string email, CancellationToken ct)
    {
        string normalizedEmail = email.ToUpperInvariant();

        WallowUser? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            return;
        }

        Membership? membership = await membershipRepository.GetAsync(user.Id, organizationId, ct);

        if (membership?.Status == MembershipStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.AlreadyAMember",
                "That email address already belongs to this organization");
        }
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
            Guid roleId = await defaultRoleResolver.ResolveAsync(organizationId, ct);
            membershipRepository.Add(Membership.Enroll(
                userId, OrganizationId.Create(organizationId), roleId, timeProvider));
            return true;
        }

        switch (membership.Status)
        {
            // An access request the invitation supersedes. Leaving it Pending would strand a row
            // that blocks the next legitimate request and outlives a later denial.
            case MembershipStatus.Pending:
                membership.Approve(await defaultRoleResolver.ResolveAsync(organizationId, ct), userId, timeProvider);
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
