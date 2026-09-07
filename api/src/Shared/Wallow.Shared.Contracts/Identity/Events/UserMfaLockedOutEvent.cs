// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user is locked out due to repeated MFA failures.
/// Consumed by Identity audit logging.
/// </summary>
public sealed record UserMfaLockedOutEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public Guid? TenantId { get; init; }
    public required int LockoutCount { get; init; }
}
