// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user regenerates their MFA backup codes.
/// </summary>
public sealed record UserMfaBackupCodesRegeneratedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public Guid? TenantId { get; init; }
}
