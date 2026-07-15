// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an admin clears a user's MFA lockout.
/// Consumers: Audit logging
/// </summary>
public sealed record UserMfaLockoutClearedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ClearedByUserId { get; init; }
}
