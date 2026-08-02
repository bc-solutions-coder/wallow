using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Public because Wolverine's generated handlers construct their dependencies inline and
/// <c>ServiceLocationPolicy.NotAllowed</c> turns a non-public concrete type into a codegen
/// failure at the first message.
/// </summary>
public sealed partial class MembershipReviewService(
    IMembershipRepository memberships,
    IdentityDbContext dbContext,
    IDefaultMemberRoleResolver defaultRoleResolver,
    IMembershipAccessRevoker accessRevoker,
    ILastOwnerGuard lastOwnerGuard,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    ILogger<MembershipReviewService> logger) : IMembershipReviewService
{
    public async Task<IReadOnlyList<PendingMembershipDto>> GetPendingAsync(
        Guid organizationId, CancellationToken ct = default)
    {
        IReadOnlyList<Membership> pending = await memberships.GetForOrganizationAsync(
            organizationId, MembershipStatus.Pending, ct);

        if (pending.Count == 0)
        {
            return [];
        }

        List<Guid> requesterIds = [.. pending.Select(m => m.UserId)];
        Dictionary<Guid, WallowUser> users = await dbContext.Users
            .Where(u => requesterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return
        [
            .. pending
                .OrderBy(m => m.RequestedAt ?? DateTimeOffset.MaxValue)
                .Where(m => users.ContainsKey(m.UserId))
                .Select(m => new PendingMembershipDto(
                    m.UserId,
                    users[m.UserId].Email ?? string.Empty,
                    users[m.UserId].FirstName,
                    users[m.UserId].LastName,
                    m.RequestedAt))
        ];
    }

    public async Task ApproveAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        // The organization's default, never a role the requester asked for or holds elsewhere:
        // roles are granted by an organization and carry no authority outside it.
        Guid roleId = await defaultRoleResolver.ResolveAsync(organizationId, ct);

        membership.Approve(roleId, actorId, timeProvider);
        await memberships.SaveChangesAsync(ct);

        // The same event a directly-added member raises, so the welcome mail an approved requester
        // gets is the one every new member gets.
        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            Email = await GetEmailAsync(userId, ct)
        });

        await PublishTransitionAsync(MembershipTransition.Approved, organizationId, userId, actorId);

        LogMembershipApproved(userId, organizationId, actorId);
    }

    public async Task DenyAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        membership.Deny(actorId, timeProvider);
        await memberships.SaveChangesAsync(ct);

        // No revocation: only a Pending membership can be denied, and a Pending membership never
        // authenticated, so there is nothing issued against this organization to take away.
        await PublishTransitionAsync(MembershipTransition.Denied, organizationId, userId, actorId);

        LogMembershipDenied(userId, organizationId, actorId);
    }

    public async Task ClearDenialAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        if (membership.Status != MembershipStatus.Denied)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotDenied",
                "Only a denied membership can have its denial cleared");
        }

        // Nothing to revoke and nothing to announce: a denied membership never authenticated here.
        memberships.Remove(membership);
        await memberships.SaveChangesAsync(ct);

        await PublishTransitionAsync(MembershipTransition.DenialCleared, organizationId, userId, actorId);

        LogDenialCleared(userId, organizationId, actorId);
    }

    public async Task SuspendAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        // Suspension ends an active membership, so it is a departure as far as ownership is
        // concerned: an organization whose only owner is suspended has no owner.
        await lastOwnerGuard.ExecuteDepartureAsync(organizationId, userId, async token =>
        {
            membership.Suspend(actorId, timeProvider);
            await memberships.SaveChangesAsync(token);
        }, ct);

        // The status alone only decides the NEXT sign-in. Everything already issued off the
        // membership — tokens, open streams — outlives it unless it is taken away here.
        await accessRevoker.RevokeAsync(userId, organizationId, ct);

        await PublishTransitionAsync(MembershipTransition.Suspended, organizationId, userId, actorId);

        LogMembershipSuspended(userId, organizationId, actorId);
    }

    public async Task ReinstateAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        membership.Reinstate(actorId, timeProvider);
        await memberships.SaveChangesAsync(ct);

        await PublishTransitionAsync(MembershipTransition.Reinstated, organizationId, userId, actorId);

        LogMembershipReinstated(userId, organizationId, actorId);
    }

    public async Task LeaveAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);
        string email = await GetEmailAsync(userId, ct);

        // Deleted, not marked: nobody reviewed this, so there is no decision worth keeping, and a
        // leftover row would read as a refusal the next time they ask to join.
        await lastOwnerGuard.ExecuteDepartureAsync(organizationId, userId, async token =>
        {
            memberships.Remove(membership);
            await memberships.SaveChangesAsync(token);
        }, ct);

        await accessRevoker.RevokeAsync(userId, organizationId, ct);

        // The same event removal by an administrator raises: from every other module's side of the
        // boundary, why the person stopped being a member is not a distinction that changes anything.
        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            Email = email
        });

        // Nobody acted on the leaver's behalf, so the actor is the leaver. Left blank it would read
        // as a removal whose author was lost.
        await PublishTransitionAsync(MembershipTransition.Left, organizationId, userId, userId);

        LogMembershipLeft(userId, organizationId);
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

    private async Task<Membership> RequireMembershipAsync(
        Guid organizationId, Guid userId, CancellationToken ct)
    {
        Membership? membership = await memberships.GetAsync(userId, organizationId, ct);

        return membership ?? throw new BusinessRuleException(
            "Identity.MemberNotFound",
            "User is not a member of this organization");
    }

    private async Task<string> GetEmailAsync(Guid userId, CancellationToken ct)
    {
        WallowUser? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.Email ?? string.Empty;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership approved: userId={UserId}, organizationId={OrganizationId}, by={ActorId}")]
    private partial void LogMembershipApproved(Guid userId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership denied: userId={UserId}, organizationId={OrganizationId}, by={ActorId}")]
    private partial void LogMembershipDenied(Guid userId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership denial cleared: userId={UserId}, organizationId={OrganizationId}, by={ActorId}")]
    private partial void LogDenialCleared(Guid userId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership suspended: userId={UserId}, organizationId={OrganizationId}, by={ActorId}")]
    private partial void LogMembershipSuspended(Guid userId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership reinstated: userId={UserId}, organizationId={OrganizationId}, by={ActorId}")]
    private partial void LogMembershipReinstated(Guid userId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership left: userId={UserId}, organizationId={OrganizationId}")]
    private partial void LogMembershipLeft(Guid userId, Guid organizationId);
}
