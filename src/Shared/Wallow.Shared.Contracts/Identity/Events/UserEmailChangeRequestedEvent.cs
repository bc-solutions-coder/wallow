// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user requests an email change.
/// Consumers: Notifications (confirmation email to new address)
/// </summary>
public sealed record UserEmailChangeRequestedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string NewEmail { get; init; }
    public required string ConfirmationUrl { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
