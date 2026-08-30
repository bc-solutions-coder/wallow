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

    /// <summary>
    /// Declares one of the platform's own clients, bound from the "firstParty" key. First-party
    /// status is a seed-only property: it is never inferred from the client id, and it is what
    /// makes the seeder register the client with OpenIddict's implicit consent type. A
    /// first-party client is bound to no organization, so <see cref="TenantId"/>,
    /// <see cref="TenantName"/>, <see cref="SeedMembers"/> and <see cref="SeedMemberRoles"/>
    /// are rejected on one.
    /// </summary>
    public bool FirstParty { get; init; }

    public bool IsBoundToOrganization =>
        (TenantId.HasValue && TenantId.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(TenantName);
}

public sealed class PreRegisteredClientOptions
{
    public const string SectionName = "PreRegisteredClients";

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];

    // Fails fast, before any client is written, on the two seed shapes that would otherwise
    // register something the platform does not mean: a secret-less client with no explicit
    // public declaration, and a client on the wrong side of the client <-> organization
    // invariant (first-party => no organization; every other client => exactly one).
    public void Validate()
    {
        List<string> silentlyPublic = Clients
            .Where(c => string.IsNullOrWhiteSpace(c.Secret) && c.Public != true)
            .Select(c => c.ClientId)
            .ToList();

        if (silentlyPublic.Count > 0)
        {
            throw new InvalidOperationException(
                "Pre-registered client(s) " + string.Join(", ", silentlyPublic) + " have no secret and do not declare "
                + "\"public\": true. A missing secret never implies a public client: declare \"public\": true for "
                + "browser/native clients, or supply the secret for confidential ones.");
        }

        List<string> boundFirstParty = Clients
            .Where(c => c.FirstParty && c.IsBoundToOrganization)
            .Select(c => c.ClientId)
            .ToList();

        List<string> firstPartyWithMembers = Clients
            .Where(c => c.FirstParty && (c.SeedMembers.Count > 0 || c.SeedMemberRoles.Count > 0))
            .Select(c => c.ClientId)
            .ToList();

        List<string> unboundThirdParty = Clients
            .Where(c => !c.FirstParty && !c.IsBoundToOrganization)
            .Select(c => c.ClientId)
            .ToList();

        if (boundFirstParty.Count == 0 && firstPartyWithMembers.Count == 0 && unboundThirdParty.Count == 0)
        {
            return;
        }

        List<string> problems = [];
        if (boundFirstParty.Count > 0)
        {
            problems.Add(
                "first-party client(s) " + string.Join(", ", boundFirstParty) + " declare an organization "
                + "(\"tenantId\"/\"tenantName\"); a first-party client is bound to no organization");
        }

        if (firstPartyWithMembers.Count > 0)
        {
            problems.Add(
                "first-party client(s) " + string.Join(", ", firstPartyWithMembers) + " declare \"seedMembers\"/"
                + "\"seedMemberRoles\"; seed members belong to an organization-bound client");
        }

        if (unboundThirdParty.Count > 0)
        {
            problems.Add(
                "client(s) " + string.Join(", ", unboundThirdParty) + " declare no organization; a client that "
                + "is not \"firstParty\": true must name exactly one (\"tenantId\" or \"tenantName\")");
        }

        throw new InvalidOperationException(
            "Pre-registered clients violate the client/organization invariant: "
            + string.Join("; ", problems) + ".");
    }
}
