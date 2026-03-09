// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user is added to an organization.
/// Consumers: Communications (member welcome)
/// </summary>
public sealed record OrganizationMemberAddedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}
