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
    /// Optional front-channel logout URL loaded by the end-session page. Omission disables this channel.
    /// </summary>
    public string? FrontchannelLogoutUri { get; init; }

    /// <summary>
    /// Optional URL for signed back-channel logout tokens. Must be absolute and fragment-free;
    /// HTTP is allowed only for confidential clients.
    /// </summary>
    public string? BackchannelLogoutUri { get; init; }

    /// <summary>
    /// Stored OIDC session-required metadata. Wallow includes sid in every logout token regardless of this flag.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; init; }

    public Collection<string> Scopes { get; init; } = [];

    /// <summary>
    /// Refresh-token lifetime in seconds. Client sync pins a first-party or third-party default
    /// when omitted; service accounts ignore this setting.
    /// </summary>
    public int? RefreshTokenLifetime { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public Collection<string> SeedMembers { get; init; } = [];

    /// <summary>
    /// Role names for newly added seed members, keyed by email. Missing entries use user;
    /// existing memberships are left unchanged.
    /// </summary>
    public Dictionary<string, string> SeedMemberRoles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // Clients without a secret must explicitly declare themselves public.
    public bool? Public { get; init; }

    public bool IsPublic => Public == true;

    /// <summary>
    /// Marks a platform client with implicit consent. First-party clients cannot specify
    /// <see cref="TenantId"/>, <see cref="TenantName"/>, <see cref="SeedMembers"/>, or <see cref="SeedMemberRoles"/>.
    /// </summary>
    public bool FirstParty { get; init; }

    public bool IsBoundToOrganization =>
        (TenantId.HasValue && TenantId.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(TenantName);
}

public sealed class PreRegisteredClientOptions
{
    public const string SectionName = "PreRegisteredClients";

    public Collection<PreRegisteredClientDefinition> Clients { get; set; } = [];


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

    // Validate all client definitions before sync creates, updates, or deletes registrations.
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

        List<string> badBackchannelUris = Clients
            .Where(c => c.BackchannelLogoutUri is not null
                && !ClientUriRules.TryParseBackchannelLogoutUri(c.BackchannelLogoutUri, !c.IsPublic, out _))
            .Select(c => c.ClientId + " (" + c.BackchannelLogoutUri + ")")
            .ToList();

        if (badBackchannelUris.Count > 0)
        {
            throw new InvalidOperationException(
                "Pre-registered client(s) " + string.Join(", ", badBackchannelUris) + " register a back-channel "
                + "logout URI the platform refuses. " + ClientUriRules.BackchannelLogoutUriError);
        }

        List<string> badLifetimes = Clients
            .Where(c => c.RefreshTokenLifetime is { } lifetime && !ClientRefreshTokenLifetimes.IsInRange(lifetime))
            .Select(c => c.ClientId)
            .ToList();

        if (badLifetimes.Count > 0)
        {
            throw new InvalidOperationException(
                "Pre-registered client(s) " + string.Join(", ", badLifetimes)
                + " declare a \"refreshTokenLifetime\" outside the accepted range of "
                + $"{ClientRefreshTokenLifetimes.MinimumSeconds} to {ClientRefreshTokenLifetimes.MaximumSeconds} seconds.");
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
