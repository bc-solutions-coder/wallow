using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class UserEnrollmentService(
    IdentityDbContext dbContext,
    IMembershipRepository memberships,
    IDefaultMemberRoleResolver defaultRoleResolver,
    IAccessRequestRecipientResolver recipientResolver,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    ILogger<UserEnrollmentService> logger) : IUserEnrollmentService
{
    public async Task<EnrollmentOutcome> EnrollAsync(
        Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        // Reuse existing membership state instead of creating a second request row.
        Membership? existing = await memberships.GetAsync(userId, organizationId, ct);

        // Once denial expires, the current enrollment policy decides what replaces it.
        Membership? spentDenial = existing is { Status: MembershipStatus.Denied }
            && !existing.IsWithinDenialCooldown(timeProvider)
                ? existing
                : null;

        if (existing is not null && spentDenial is null)
        {
            return FromExisting(existing.Status);
        }

        Organization? organization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == OrganizationId.Create(organizationId), ct);

        if (organization is not { IsActive: true })
        {
            return new Rejected(EnrollmentReasons.NotAMember);
        }

        WallowUser? user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
        {
            return new Rejected(EnrollmentReasons.NotAMember);
        }

        // Verify email before applying policy to a new membership or spent denial.
        // Existing active/pending memberships returned earlier.
        if (!user.EmailConfirmed)
        {
            LogEnrollmentRefusedUnverifiedEmail(userId, organizationId);
            return new Rejected(EnrollmentReasons.EmailUnverified);
        }

        EnrollmentPolicy policy = await ResolvePolicyAsync(organizationId, ct);
        LogEnrollmentPolicyApplied(userId, organizationId, policy);

        switch (policy)
        {
            case EnrollmentPolicy.Open:
                return await EnrollDirectlyAsync(user, organization, spentDenial, ct);

            case EnrollmentPolicy.RequestApproval:
                return await RecordRequestAsync(user, organization, spentDenial, ct);

            // InviteOnly and unknown policies refuse self-service enrollment.
            default:
                return new Rejected(EnrollmentReasons.NotAMember);
        }
    }

    private async Task<EnrollmentOutcome> EnrollDirectlyAsync(
        WallowUser user, Organization organization, Membership? spentDenial, CancellationToken ct)
    {
        Guid organizationId = organization.Id.Value;

        // Resolve this organization's default role rather than inheriting roles from elsewhere.
        Guid roleId = await defaultRoleResolver.ResolveAsync(organizationId, ct);

        if (spentDenial is null)
        {
            memberships.Add(Membership.Enroll(user.Id, organization.Id, roleId, timeProvider));
        }
        else
        {
            spentDenial.EnrollAgain(roleId, timeProvider);
        }

        await memberships.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = user.Id,
            Email = user.Email ?? string.Empty
        });

        await PublishTransitionAsync(MembershipTransition.Enrolled, organizationId, user.Id);

        LogEnrolled(user.Id, organizationId);
        return new Enrolled();
    }

    private async Task<EnrollmentOutcome> RecordRequestAsync(
        WallowUser user, Organization organization, Membership? spentDenial, CancellationToken ct)
    {
        Guid organizationId = organization.Id.Value;

        if (spentDenial is null)
        {
            memberships.Add(Membership.RequestAccess(user.Id, organization.Id, timeProvider));
        }
        else
        {
            spentDenial.RequestAgain(timeProvider);
        }

        await memberships.SaveChangesAsync(ct);

        IReadOnlyList<string> recipients = await recipientResolver.ResolveAsync(organizationId, ct);
        if (recipients.Count == 0)
        {
            // The saved pending membership remains the request record even without email recipients.
            LogAccessRequestHasNoRecipients(user.Id, organizationId);
        }

        await messageBus.PublishAsync(new AccessRequestedEvent
        {
            TenantId = organizationId,
            OrganizationName = organization.Name,
            RequesterUserId = user.Id,
            RequesterEmail = user.Email ?? string.Empty,
            RequesterName = $"{user.FirstName} {user.LastName}".Trim(),
            RecipientEmails = recipients
        });

        await PublishTransitionAsync(MembershipTransition.AccessRequested, organizationId, user.Id);

        LogAccessRequested(user.Id, organizationId, recipients.Count);
        return new PendingApproval();
    }

    /// <summary>
    /// Self-service enrollment records the same user as actor and subject.
    /// </summary>
    private ValueTask PublishTransitionAsync(
        MembershipTransition transition, Guid organizationId, Guid userId) =>
        messageBus.PublishAsync(new MembershipTransitionedEvent
        {
            Transition = transition,
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            ActorId = userId,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime
        });

    /// <summary>
    /// Missing settings default to InviteOnly, disabling self-service enrollment.
    /// </summary>
    private async Task<EnrollmentPolicy> ResolvePolicyAsync(Guid organizationId, CancellationToken ct)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        OrganizationSettings? settings = await dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

        return settings?.EnrollmentPolicy ?? EnrollmentPolicy.InviteOnly;
    }

    /// <summary>
    /// Maps existing status after the caller has excluded spent denials.
    /// Suspension and a standing denial remain refusals.
    /// </summary>
    private static EnrollmentOutcome FromExisting(MembershipStatus status) => status switch
    {
        MembershipStatus.Active => new Enrolled(),
        MembershipStatus.Pending => new PendingApproval(),
        MembershipStatus.Suspended => new Rejected(EnrollmentReasons.MembershipSuspended),
        MembershipStatus.Denied => new Rejected(EnrollmentReasons.MembershipDenied),
        _ => new Rejected(EnrollmentReasons.NotAMember)
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment policy applied: userId={UserId}, organizationId={OrganizationId}, policy={Policy}")]
    private partial void LogEnrollmentPolicyApplied(Guid userId, Guid organizationId, EnrollmentPolicy policy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrolled user {UserId} in organization {OrganizationId}")]
    private partial void LogEnrolled(Guid userId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Access requested by user {UserId} for organization {OrganizationId}, recipients={RecipientCount}")]
    private partial void LogAccessRequested(Guid userId, Guid organizationId, int recipientCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access request by user {UserId} for organization {OrganizationId} has no recipients to notify")]
    private partial void LogAccessRequestHasNoRecipients(Guid userId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Enrollment refused for user {UserId} in organization {OrganizationId}: email not verified")]
    private partial void LogEnrollmentRefusedUnverifiedEmail(Guid userId, Guid organizationId);
}
