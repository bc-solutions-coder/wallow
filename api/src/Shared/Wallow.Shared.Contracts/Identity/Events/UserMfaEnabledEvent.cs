// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user successfully enables MFA on their account.
/// Consumers: Notifications (confirmation email), Audit logging
/// </summary>
public sealed record UserMfaEnabledEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
}
