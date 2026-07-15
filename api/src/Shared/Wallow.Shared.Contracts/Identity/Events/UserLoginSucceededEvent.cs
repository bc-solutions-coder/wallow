// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user successfully logs in.
/// Consumers: Audit logging
/// </summary>
public sealed record UserLoginSucceededEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public string? IpAddress { get; init; }
}
