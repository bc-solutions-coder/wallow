// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user session is forcibly evicted.
/// Consumers: Audit logging, Notifications
/// </summary>
public sealed record UserSessionEvictedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid SessionId { get; init; }
    public required string Reason { get; init; }
}
