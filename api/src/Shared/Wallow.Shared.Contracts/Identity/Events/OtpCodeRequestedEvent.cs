// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user requests a one-time password for passwordless login.
/// Consumers: Communications (OTP email)
/// </summary>
public sealed record OtpCodeRequestedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string Code { get; init; }
}
