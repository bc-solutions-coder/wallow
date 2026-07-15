// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user successfully changes their password.
/// Consumers: Notifications (confirmation email)
/// </summary>
public sealed record PasswordChangedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
}
