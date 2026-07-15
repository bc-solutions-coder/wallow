// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user login attempt fails.
/// Consumers: Audit logging
/// </summary>
public sealed record UserLoginFailedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public string? IpAddress { get; init; }
    public required string Reason { get; init; }
}
