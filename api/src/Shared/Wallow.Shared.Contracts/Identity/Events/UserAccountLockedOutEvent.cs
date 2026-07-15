// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user account is locked out due to repeated failed login attempts.
/// Consumers: Audit logging, Notifications
/// </summary>
public sealed record UserAccountLockedOutEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public string? IpAddress { get; init; }
}
