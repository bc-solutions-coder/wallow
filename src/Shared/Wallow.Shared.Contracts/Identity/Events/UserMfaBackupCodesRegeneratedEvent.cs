// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user regenerates their MFA backup codes.
/// Consumers: Notifications (security alert), Audit logging
/// </summary>
public sealed record UserMfaBackupCodesRegeneratedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
}
