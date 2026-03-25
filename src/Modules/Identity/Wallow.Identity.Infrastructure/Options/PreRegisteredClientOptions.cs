using System.Collections.ObjectModel;

namespace Wallow.Identity.Infrastructure.Options;

public sealed record PreRegisteredClientDefinition
{
    public string ClientId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Secret { get; init; }

    public Collection<string> RedirectUris { get; init; } = [];

    public Collection<string> PostLogoutRedirectUris { get; init; } = [];

    public Collection<string> Scopes { get; init; } = [];

    public bool IsPublic => string.IsNullOrEmpty(Secret);
}

public sealed class PreRegisteredClientOptions
{
    public const string SectionName = "PreRegisteredClients";

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];
}
