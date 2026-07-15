// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user's email address has been changed.
/// Consumers: Notifications (confirmation to old and new address)
/// </summary>
public sealed record UserEmailChangedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string OldEmail { get; init; }
    public required string NewEmail { get; init; }
}
