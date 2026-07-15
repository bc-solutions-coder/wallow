using System.Collections.ObjectModel;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.SeederService;

public sealed class SeedOptions
{
    public Collection<string> Roles { get; set; } = [];

    public Collection<SeedApiScope> ApiScopes { get; set; } = [];

    public AdminBootstrapOptions? Admin { get; set; }

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];
}

public sealed class SeedApiScope
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}
