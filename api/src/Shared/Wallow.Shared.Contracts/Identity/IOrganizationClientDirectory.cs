namespace Wallow.Shared.Contracts.Identity;

/// <summary>
/// Client kinds exposed across module boundaries without Identity domain dependencies.
/// </summary>
public enum OrganizationClientKind
{
    Application,
    ServiceAccount,
}

/// <summary>A client as the directory answers it: who it is and which organization owns it.</summary>
public sealed record OrganizationClientInfo(string ClientId, Guid OrganizationId, OrganizationClientKind Kind);

/// <summary>
/// Cross-module client ownership and management checks for organization client sub-resources.
/// </summary>
public interface IOrganizationClientDirectory
{
    /// <summary>
    /// Returns null for both missing clients and clients owned by another organization.
    /// </summary>
    Task<OrganizationClientInfo?> FindAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Whether the user has permission to manage clients in this organization.
    /// </summary>
    Task<bool> CanManageClientsAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
