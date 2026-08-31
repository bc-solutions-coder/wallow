namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// The OpenIddict application properties Wallow attaches to a client record, and the only names by
/// which they may be read or written.
/// </summary>
/// <remarks>
/// Both are authorization inputs resolved from the record rather than from the client id, which is
/// chosen by whoever registered the account. The names live here, below both the Api layer that
/// reads them and the Infrastructure layer that writes them, because the two cannot see each
/// other: a second spelling on either side is not a compile error, it is a service account that
/// silently resolves no tenant.
/// </remarks>
public static class ClientApplicationProperties
{
    /// <summary>
    /// Names the tenant a client belongs to. The token endpoint turns it into the <c>org_id</c>
    /// claim, which is what makes a machine caller's request resolve a tenant at all.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Marks a client as a platform operator, allowed to address another tenant through the
    /// <c>X-Tenant-Id</c> header.
    /// </summary>
    public const string IsOperator = "is_operator";

    /// <summary>
    /// The client's OIDC front-channel logout endpoint. The end-session page loads it in a
    /// hidden iframe (with <c>iss</c> and <c>sid</c>) so the RP can end its own session when
    /// the SSO session ends. Absent for clients that opted out of notification.
    /// </summary>
    public const string FrontchannelLogoutUri = "frontchannel_logout_uri";

    /// <summary>
    /// The client's OIDC back-channel logout endpoint, recorded at registration so the
    /// back-channel logout work can deliver logout tokens to it. Absent when the client has none.
    /// </summary>
    public const string BackchannelLogoutUri = "backchannel_logout_uri";

    /// <summary>
    /// The client's declaration that its logout tokens must carry a <c>sid</c> claim. Wallow
    /// always includes <c>sid</c>, so the flag changes nothing at delivery time; it is stored and
    /// echoed because the OIDC back-channel registration metadata defines it. Absent means false.
    /// </summary>
    public const string BackchannelLogoutSessionRequired = "backchannel_logout_session_required";
}
