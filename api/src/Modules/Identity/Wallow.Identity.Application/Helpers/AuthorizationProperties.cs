namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Shared property keys for OpenIddict authorization records.
/// </summary>
public static class AuthorizationProperties
{
    /// <summary>
    /// Organization used for sign-in, retained for revocation even when the client is unbound.
    /// </summary>
    public const string OrganizationId = "org_id";

    /// <summary>
    /// OIDC session ID on a per-login authorization, used to revoke one browser session
    /// without revoking the user's other sessions.
    /// </summary>
    public const string SessionId = "sid";
}
