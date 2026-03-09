// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user's role changes.
/// Consumers: Communications (notify user of role change)
/// </summary>
public sealed record UserRoleChangedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string OldRole { get; init; }
    public required string NewRole { get; init; }
}
