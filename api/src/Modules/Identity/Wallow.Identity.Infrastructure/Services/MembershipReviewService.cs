using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class MembershipReviewService(
    IMembershipRepository memberships,
    IdentityDbContext dbContext,
    IDefaultMemberRoleResolver defaultRoleResolver,
    IAccessRevoker accessRevoker,
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

    public Task<IReadOnlyList<ReviewedMembershipDto>> GetSuspendedAsync(
        Guid organizationId, CancellationToken ct = default) =>
        // Suspension has no dedicated timestamp; the listing uses the mutable audit stamp.
        ListReviewedAsync(organizationId, MembershipStatus.Suspended, SuspendedAt, ct);

    public Task<IReadOnlyList<ReviewedMembershipDto>> GetDeniedAsync(
        Guid organizationId, CancellationToken ct = default) =>
        ListReviewedAsync(organizationId, MembershipStatus.Denied, m => m.ReviewedAt, ct);

    public async Task ApproveAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        // Use this organization's default role, not a role requested or held elsewhere.
        Guid roleId = await defaultRoleResolver.ResolveAsync(organizationId, ct);

        membership.Approve(roleId, actorId, timeProvider);
        await memberships.SaveChangesAsync(ct);

        // Reuse the member-added notification event for approved requests.
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

        // Denial accepts only Pending memberships, so this path performs no revocation.
        await PublishTransitionAsync(MembershipTransition.Denied, organizationId, userId, actorId);

        LogMembershipDenied(userId, organizationId, actorId);
    }

    public async Task ClearDenialAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        if (membership.Status != MembershipStatus.Denied)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotDenied, "Only a denied membership can have its denial cleared");
        }

        // Remove the denied row so a new request can be evaluated immediately.
        memberships.Remove(membership);
        await memberships.SaveChangesAsync(ct);

        await PublishTransitionAsync(MembershipTransition.DenialCleared, organizationId, userId, actorId);

        LogDenialCleared(userId, organizationId, actorId);
    }

    public async Task SuspendAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        Membership membership = await RequireMembershipAsync(organizationId, userId, ct);

        // Suspending the sole active owner would leave the organization without an active owner.
        await lastOwnerGuard.ExecuteDepartureAsync(organizationId, userId, async token =>
        {
            membership.Suspend(actorId, timeProvider);
            await memberships.SaveChangesAsync(token);
        }, ct);

        // A saved status change does not itself close streams or revoke issued tokens.
        await accessRevoker.RevokeMembershipAsync(userId, organizationId, ct);

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

        // Delete the membership so leaving does not block a later enrollment request.
        await lastOwnerGuard.ExecuteDepartureAsync(organizationId, userId, async token =>
        {
            memberships.Remove(membership);
            await memberships.SaveChangesAsync(token);
        }, ct);

        await accessRevoker.RevokeMembershipAsync(userId, organizationId, ct);

        // Publish the shared member-removal event as well as the specific Left transition.
        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            Email = email
        });

        // Self-service departure records the leaver as both actor and subject.
        await PublishTransitionAsync(MembershipTransition.Left, organizationId, userId, userId);

        LogMembershipLeft(userId, organizationId);
    }

    /// <summary>
    /// Lists reviewed memberships using the status-specific timestamp selector.
    /// </summary>
    private async Task<IReadOnlyList<ReviewedMembershipDto>> ListReviewedAsync(
        Guid organizationId,
        MembershipStatus status,
        Func<Membership, DateTimeOffset?> statusChangedAt,
        CancellationToken ct)
    {
        IReadOnlyList<Membership> reviewed = await memberships.GetForOrganizationAsync(
            organizationId, status, ct);

        if (reviewed.Count == 0)
        {
            return [];
        }

        List<Guid> reviewedIds = [.. reviewed.Select(m => m.UserId)];
        Dictionary<Guid, WallowUser> users = await dbContext.Users
            .Where(u => reviewedIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return
        [
            .. reviewed
                .OrderByDescending(m => statusChangedAt(m) ?? DateTimeOffset.MinValue)
                .Where(m => users.ContainsKey(m.UserId))
                .Select(m => new ReviewedMembershipDto(
                    m.UserId,
                    users[m.UserId].Email ?? string.Empty,
                    users[m.UserId].FirstName,
                    users[m.UserId].LastName,
                    m.Status,
                    statusChangedAt(m)))
        ];
    }

    private static DateTimeOffset? SuspendedAt(Membership membership) =>
        membership.UpdatedAt is { } updatedAt
            ? new DateTimeOffset(DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc))
            : null;

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

        return membership ?? throw new BusinessRuleException(IdentityErrors.MemberNotFound);
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
