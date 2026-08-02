// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a new user registers.
/// Consumers: Notifications (welcome email, setup)
/// </summary>
public sealed record UserRegisteredEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public Guid? TenantId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PhoneNumber { get; init; }
}
