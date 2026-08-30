namespace Wallow.Shared.Contracts.Identity;

/// <summary>
/// The kind of client an organization registers. Mirrors Identity's internal enum on purpose:
/// outside the seam a consumer only ever needs to tell an application from a service account.
/// </summary>
public enum OrganizationClientKind
{
    Application,
    ServiceAccount,
}

/// <summary>A client as the directory answers it: who it is and which organization owns it.</summary>
public sealed record OrganizationClientInfo(string ClientId, Guid OrganizationId, OrganizationClientKind Kind);

/// <summary>
/// Identity's public answer to "does client X belong to organization Y". A module that hangs a
/// sub-resource off the org-scoped client surface (Branding) resolves ownership through this
/// contract, never through OpenIddict or Identity's persistence, per the identity seam ADR.
/// </summary>
public interface IOrganizationClientDirectory
{
    /// <summary>
    /// The organization's client, or <see langword="null"/> when no such client exists or it
    /// belongs to a different organization — indistinguishable on purpose, so consumers answer
    /// both as not found.
    /// </summary>
    Task<OrganizationClientInfo?> FindAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Whether the user's membership of the organization grants management of its clients —
    /// Identity's cross-organization reach answer, mirrored here so a sub-resource of the
    /// org-scoped client surface admits exactly the callers the parent surface admits.
    /// </summary>
    Task<bool> CanManageClientsAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
