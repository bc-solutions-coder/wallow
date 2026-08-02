// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published whenever somebody's membership of an organization changes state.
/// Consumers: Identity (auth audit trail).
/// </summary>
/// <remarks>
/// One event with a <see cref="Transition"/> discriminator rather than one record per transition:
/// every transition carries the same four facts and differs only in which one happened, so a
/// record apiece would be fourteen copies of one shape and fourteen handlers to keep in step.
/// <para>
/// <see cref="ActorId"/> is who made the change and <see cref="UserId"/> is who it was made about.
/// They are equal for the transitions somebody performs on their own membership — requesting
/// access, enrolling, leaving — and that equality is the record, not an omission.
/// </para>
/// </remarks>
public sealed record MembershipTransitionedEvent : IntegrationEvent
{
    public required MembershipTransition Transition { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }

    /// <summary>The member the transition is about.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Who made the change.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>The role granted or taken away, for the two role transitions only.</summary>
    public string? RoleName { get; init; }
}
