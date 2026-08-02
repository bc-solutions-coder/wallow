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

/// <summary>
/// Public because Wolverine's generated handlers construct their dependencies inline and
/// <c>ServiceLocationPolicy.NotAllowed</c> turns a non-public concrete type into a codegen
/// failure at the first message.
/// </summary>
public sealed partial class UserEnrollmentService(
    IdentityDbContext dbContext,
    IMembershipRepository memberships,
    IDefaultMemberRoleResolver defaultRoleResolver,
    IAccessRequestRecipientResolver recipientResolver,
    IMessageBus messageBus,
    TimeProvider timeProvider,
    ILogger<UserEnrollmentService> logger) : IUserEnrollmentService
{
    /// <summary>The error-screen reasons a refusal routes on.</summary>
    private const string NotAMember = "not_a_member";
    private const string EmailUnverified = "email_unverified";

    public async Task<EnrollmentOutcome> EnrollAsync(
        Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        // An existing row decides on its own terms. Reading it first is also what makes a repeat
        // authorize idempotent: a second Pending row would outlive the first one's denial and
        // hand the person a fresh request every time they retried.
        Membership? existing = await memberships.GetAsync(userId, organizationId, ct);

        // A denial that has run its course stops being the organization's answer. What replaces it
        // is not a second chance of its own: the current policy decides, exactly as for a stranger.
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
            return new Rejected(NotAMember);
        }

        WallowUser? user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
        {
            return new Rejected(NotAMember);
        }

        // Required under every policy, not only the self-service ones: an unverified address is
        // an unproven claim to an identity, and a second membership is exactly what somebody who
        // typed a stranger's address at signup would be reaching for.
        if (!user.EmailConfirmed)
        {
            LogEnrollmentRefusedUnverifiedEmail(userId, organizationId);
            return new Rejected(EmailUnverified);
        }

        EnrollmentPolicy policy = await ResolvePolicyAsync(organizationId, ct);
        LogEnrollmentPolicyApplied(userId, organizationId, policy);

        switch (policy)
        {
            case EnrollmentPolicy.Open:
                return await EnrollDirectlyAsync(user, organization, spentDenial, ct);

            case EnrollmentPolicy.RequestApproval:
                return await RecordRequestAsync(user, organization, spentDenial, ct);

            // InviteOnly, and any policy a fork adds without deciding what it means here.
            default:
                return new Rejected(NotAMember);
        }
    }

    private async Task<EnrollmentOutcome> EnrollDirectlyAsync(
        WallowUser user, Organization organization, Membership? spentDenial, CancellationToken ct)
    {
        Guid organizationId = organization.Id.Value;

        // Always the organization's default, never a role inherited from another membership:
        // roles are granted by an organization and carry no authority outside it.
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
            // The membership row is the durable record, so a request nobody can be told about is
            // still a request. Someone will find it on the pending list.
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
    /// Enrolment is self-service, so the actor and the subject are the same person. Recording that
    /// equality is the point: a blank actor would read as an unattributed admission.
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
    /// An organization that has never been configured admits nobody: a policy nobody has chosen
    /// must be the one that grants nothing.
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
    /// The outcome an existing membership already describes. Suspended and Denied are reviewed
    /// refusals — reversing one is a decision an administrator makes explicitly, never something
    /// the refused person triggers by signing in again.
    /// </summary>
    private static EnrollmentOutcome FromExisting(MembershipStatus status) => status switch
    {
        MembershipStatus.Active => new Enrolled(),
        MembershipStatus.Pending => new PendingApproval(),
        MembershipStatus.Suspended => new Rejected("membership_suspended"),
        MembershipStatus.Denied => new Rejected("membership_denied"),
        _ => new Rejected(NotAMember)
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
