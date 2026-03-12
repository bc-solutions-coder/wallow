namespace Foundry.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user is removed from an organization.
/// Consumers: Communications (access revoked notice)
/// </summary>
public sealed record OrganizationMemberRemovedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}
