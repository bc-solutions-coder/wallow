using System.Collections.ObjectModel;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.SeederService;

public sealed class SeedOptions
{
    public Collection<string> Roles { get; set; } = [];

    public Collection<SeedApiScope> ApiScopes { get; set; } = [];

    public AdminBootstrapOptions? Admin { get; set; }

    public Collection<SeedOrganizationDefinition> Organizations { get; set; } = [];

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];

    /// <summary>
    /// Client-id-keyed secret overrides, such as ClientSecrets__&lt;clientId&gt; environment variables.
    /// Blank values are ignored; nonblank values for unknown clients fail option binding.
    /// </summary>
    public Dictionary<string, string> ClientSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SeedApiScope
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool PlatformOnly { get; set; }
}
