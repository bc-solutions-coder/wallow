// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user submits a membership request based on email domain.
/// Consumers: Notifications (admin notification of pending request)
/// </summary>
public sealed record MembershipRequestCreatedEvent : IntegrationEvent
{
    public required Guid MembershipRequestId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string EmailDomain { get; init; }
}
