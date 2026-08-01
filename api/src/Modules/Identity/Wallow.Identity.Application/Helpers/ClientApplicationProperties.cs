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
}
