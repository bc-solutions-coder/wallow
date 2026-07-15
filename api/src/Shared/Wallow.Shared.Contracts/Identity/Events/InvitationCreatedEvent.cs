// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a new invitation is created.
/// Consumers: Communications (invitation email)
/// </summary>
public sealed record InvitationCreatedEvent : IntegrationEvent
{
    public required Guid InvitationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
