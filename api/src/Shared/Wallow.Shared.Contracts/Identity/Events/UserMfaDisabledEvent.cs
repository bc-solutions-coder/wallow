// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user disables MFA on their account.
/// </summary>
public sealed record UserMfaDisabledEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public Guid? TenantId { get; init; }
}
