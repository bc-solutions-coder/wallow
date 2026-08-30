using System.Collections.ObjectModel;
using Wallow.Identity.Application.Helpers;

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

    // The client <-> organization invariant as (predicate, message) rules: each names the
    // offending clients between its two message halves so one report lists every violation.
    private static readonly (Func<PreRegisteredClientDefinition, bool> Violates, string Before, string After)[] _organizationRules =
    [
        (c => c.FirstParty && c.IsBoundToOrganization,
            "first-party client(s) ",
            " declare an organization (\"tenantId\"/\"tenantName\"); a first-party client is bound to no organization"),
        (c => c.FirstParty && (c.SeedMembers.Count > 0 || c.SeedMemberRoles.Count > 0),
            "first-party client(s) ",
            " declare \"seedMembers\"/\"seedMemberRoles\"; seed members belong to an organization-bound client"),
        (c => !c.FirstParty && !c.IsBoundToOrganization,
            "client(s) ",
            " declare no organization; a client that is not \"firstParty\": true must name exactly one (\"tenantId\" or \"tenantName\")"),
    ];

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

        List<string> badRedirects = Clients
            .Select(c => (c.ClientId, Refused: ClientUriRules.FirstRefusedRedirect([.. c.RedirectUris, .. c.PostLogoutRedirectUris])))
            .Where(c => c.Refused is not null)
            .Select(c => c.ClientId + " (" + c.Refused + ")")
            .ToList();

        if (badRedirects.Count > 0)
        {
            throw new InvalidOperationException(
                "Pre-registered client(s) " + string.Join(", ", badRedirects) + " register a redirect URI the "
                + "platform refuses. " + ClientUriRules.RedirectUriError);
        }

        List<string> problems = [];
        foreach ((Func<PreRegisteredClientDefinition, bool> violates, string before, string after) in _organizationRules)
        {
            List<string> offenders = Clients.Where(violates).Select(c => c.ClientId).ToList();
            if (offenders.Count > 0)
            {
                problems.Add(before + string.Join(", ", offenders) + after);
            }
        }

        if (problems.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Pre-registered clients violate the client/organization invariant: "
            + string.Join("; ", problems) + ".");
    }
}
