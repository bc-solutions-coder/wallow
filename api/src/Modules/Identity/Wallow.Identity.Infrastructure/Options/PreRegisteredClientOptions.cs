using System.Collections.ObjectModel;

namespace Wallow.Identity.Infrastructure.Options;

public sealed record PreRegisteredClientDefinition
{
    public string ClientId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Secret { get; init; }

    public Collection<string> RedirectUris { get; init; } = [];

    public Collection<string> PostLogoutRedirectUris { get; init; } = [];

    /// <summary>
    /// OIDC front-channel logout endpoint for this client, loaded in a hidden iframe by the
    /// end-session page so the RP can drop its own session when the SSO session ends. Optional:
    /// a client without one simply is not notified.
    /// </summary>
    public string? FrontchannelLogoutUri { get; init; }

    public Collection<string> Scopes { get; init; } = [];

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public Collection<string> SeedMembers { get; init; } = [];

    /// <summary>
    /// Role name granted to each seed member BY this client's organization, keyed by email.
    /// A seed member absent from the map is enrolled with <c>user</c>: roles are per
    /// (user, organization), so an unnamed one has to mean the baseline, never admin.
    /// </summary>
    public Dictionary<string, string> SeedMemberRoles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // Explicit public-client declaration, bound from the "public" key. A client registered
    // without a secret MUST declare this true; the absence of a secret never implies public.
    public bool? Public { get; init; }

    public bool IsPublic => Public == true;
}

public sealed class PreRegisteredClientOptions
{
    public const string SectionName = "PreRegisteredClients";

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];

    // Fails fast when a client is registered with no secret and no explicit public declaration.
    public void Validate()
    {
        List<string> offenders = Clients
            .Where(c => string.IsNullOrWhiteSpace(c.Secret) && c.Public != true)
            .Select(c => c.ClientId)
            .ToList();

        if (offenders.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Pre-registered client(s) " + string.Join(", ", offenders) + " have no secret and do not declare "
            + "\"public\": true. A missing secret never implies a public client: declare \"public\": true for "
            + "browser/native clients, or supply the secret for confidential ones.");
    }
}
