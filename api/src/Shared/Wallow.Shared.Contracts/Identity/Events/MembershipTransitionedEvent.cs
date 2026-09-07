// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Reports a membership transition for the Identity audit trail.
/// ActorId equals UserId for self-service requests, enrollment and departure.
/// See docs/operations/audit-events.md, Membership events.
/// </summary>
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
