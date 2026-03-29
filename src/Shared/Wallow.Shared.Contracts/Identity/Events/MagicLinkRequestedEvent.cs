// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user requests a magic link for passwordless login.
/// Consumers: Communications (magic link email)
/// </summary>
public sealed record MagicLinkRequestedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
}
