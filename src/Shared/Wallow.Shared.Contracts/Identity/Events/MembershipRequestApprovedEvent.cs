// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a membership request is approved and user is assigned to an organization.
/// Consumers: Notifications (user approval notification)
/// </summary>
public sealed record MembershipRequestApprovedEvent : IntegrationEvent
{
    public required Guid MembershipRequestId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string EmailDomain { get; init; }
}
