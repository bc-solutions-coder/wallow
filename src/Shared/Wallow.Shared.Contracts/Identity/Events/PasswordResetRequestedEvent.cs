// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user requests a password reset.
/// Consumers: Email (password reset email)
/// </summary>
public sealed record PasswordResetRequestedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string ResetToken { get; init; }
}
