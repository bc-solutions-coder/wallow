// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a new user registers.
/// Consumers: Communications (welcome email, setup)
/// </summary>
public sealed record UserRegisteredEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PhoneNumber { get; init; }
}
