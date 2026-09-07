namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Shared keys for Wallow metadata on OpenIddict applications. Authorization reads
/// these properties from the client record rather than inferring them from a client ID.
/// </summary>
public static class ClientApplicationProperties
{
    /// <summary>
    /// Client tenant used to issue the org_id claim for client-credentials requests.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Platform-operator flag permitting tenant selection through X-Tenant-Id.
    /// </summary>
    public const string IsOperator = "is_operator";

    /// <summary>
    /// Front-channel logout URL loaded by the browser with issuer and session ID parameters.
    /// </summary>
    public const string FrontchannelLogoutUri = "frontchannel_logout_uri";

    /// <summary>
    /// Back-channel endpoint for logout tokens; absent when no endpoint is configured.
    /// </summary>
    public const string BackchannelLogoutUri = "backchannel_logout_uri";

    /// <summary>
    /// Client logout-session declaration; absent means false. Wallow includes sid in logout
    /// tokens regardless of this flag, which is retained as registration metadata.
    /// </summary>
    public const string BackchannelLogoutSessionRequired = "backchannel_logout_session_required";
}
