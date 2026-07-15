// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user disables MFA on their account.
/// Consumers: Notifications (security alert), Audit logging
/// </summary>
public sealed record UserMfaDisabledEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
}
