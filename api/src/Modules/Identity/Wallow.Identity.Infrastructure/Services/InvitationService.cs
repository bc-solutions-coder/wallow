using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
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
    /// Creates or renews an invitation in the caller's tenant, preserving an outstanding
    /// token so revocation still addresses the same row.
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

        // Publish on renewal too so the same token can be emailed again.
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
    /// Creates the invitation with the caller's resolved tenant ID.
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
    /// Rejects invitations to an existing active member of the organization.
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
            throw new BusinessRuleException(IdentityErrors.AlreadyAMember);
        }
    }

    public async Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default)
    {
        InvitationId id = InvitationId.Create(invitationId);
        Invitation invitation = await invitationRepository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(IdentityErrors.InvitationNotFound, invitationId);

        invitation.Revoke(actorId, timeProvider);
        await invitationRepository.SaveChangesAsync(ct);
    }

    public Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default)
    {
        return invitationRepository.GetByTokenAsync(token, ct);
    }

    /// <summary>
    /// Accepts an invitation for its verified recipient without consulting enrollment policy.
    /// </summary>
    public async Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default)
    {
        Invitation invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new EntityNotFoundException(IdentityErrors.InvitationNotFound, token);

        await GuardInvitedIdentityAsync(invitation, userId, ct);

        try
        {
            invitation.Accept(userId, timeProvider);
        }
        catch (BusinessRuleException)
        {
            // Accept marks an expired invitation before throwing; persist that status change.
            await invitationRepository.SaveChangesAsync(ct);
            throw;
        }

        Guid organizationId = invitation.TenantId.Value;
        Membership? membership = await membershipRepository.GetAsync(userId, organizationId, ct);
        bool joined = await ApplyMembershipAsync(membership, userId, organizationId, ct);

        // Save invitation acceptance and membership together on their shared context.
        // Separate saves could consume the token without granting membership.
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
    /// Returns whether acceptance added or approved membership; false for an active member.
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
            // Acceptance approves the existing pending request rather than leaving it outstanding.
            case MembershipStatus.Pending:
                membership.Approve(await defaultRoleResolver.ResolveAsync(organizationId, ct), userId, timeProvider);
                return true;

            case MembershipStatus.Active:
                return false;

            // An invitation cannot reverse suspension or denial; that requires an explicit review.
            default:
                throw new BusinessRuleException(IdentityErrors.MembershipNotReinstatable, $"Membership of this organization is '{membership.Status}' and cannot be resumed by invitation");
        }
    }

    /// <summary>
    /// Requires a verified user whose normalized email matches the invitation,
    /// so possession of a forwarded token alone does not grant membership.
    /// </summary>
    private async Task GuardInvitedIdentityAsync(Invitation invitation, Guid userId, CancellationToken ct)
    {
        WallowUser user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new EntityNotFoundException(IdentityErrors.UserNotFound, userId);

        if (!user.EmailConfirmed)
        {
            throw new BusinessRuleException(IdentityErrors.InvitationEmailNotVerified);
        }

        // Normalize the invitation address to match Identity's stored NormalizedEmail.
        if (!string.Equals(user.NormalizedEmail, invitation.Email.ToUpperInvariant(), StringComparison.Ordinal))
        {
            throw new BusinessRuleException(IdentityErrors.InvitationEmailMismatch);
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Background cleanup must consider expired invitations across every tenant.
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
