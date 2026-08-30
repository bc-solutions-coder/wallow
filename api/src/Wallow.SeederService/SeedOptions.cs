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
    /// Client secrets keyed by clientId, injected by the deployment environment
    /// (ClientSecrets__&lt;clientId&gt; env vars) over a secret-less seed file. Name-keyed on
    /// purpose: an index-keyed secret silently lands on the wrong client the moment the seed
    /// file's order drifts from the environment's, and Compose renders an unset optional
    /// variable as an EMPTY env var, which an index key materialises as a phantom client.
    /// A blank value means "not provided" (the unset-variable artifact); a non-blank value
    /// whose key matches no seed client fails the seeder.
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
