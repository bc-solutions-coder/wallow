// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization registers a client (a developer application or a service
/// account). Consumers: Identity (auth audit trail), Branding (creates the application's
/// branding row so every application always has an end-user-facing display name).
/// </summary>
public sealed record ClientRegisteredEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>The immutable name the developer registered the client under.</summary>
    public required string ClientName { get; init; }

    public required OrganizationClientKind Kind { get; init; }

    /// <summary>Who registered it.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>Optional branded display name chosen at registration (applications only).</summary>
    public string? BrandingDisplayName { get; init; }

    /// <summary>Optional tagline chosen at registration (applications only).</summary>
    public string? BrandingTagline { get; init; }

    public string? IpAddress { get; init; }
}
