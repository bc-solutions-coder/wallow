namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Property keys Wallow stores on an OpenIddict authorization record — the entry every token a
/// sign-in issues chains back to. Both the Api and Infrastructure layers address a property
/// through the key here, which is what keeps their descriptor helpers one key per property.
/// </summary>
public static class AuthorizationProperties
{
    /// <summary>
    /// Names the organization the sign-in ran in. A bound client's organization is on the client
    /// record already; a first-party client is bound to none, so this is the only place a
    /// hint-scoped token's organization is written down where revocation can find it.
    /// </summary>
    public const string OrganizationId = "org_id";

    /// <summary>
    /// Names the SSO session the sign-in ran under — the same <c>sid</c> the id_token carries.
    /// Written on the per-login ad-hoc authorization every token chains to, so end-session can
    /// find and revoke exactly one browser session's tokens without touching the user's others.
    /// </summary>
    public const string SessionId = "sid";
}
