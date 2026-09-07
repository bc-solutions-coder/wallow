namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Resolves roles from a user's membership in a specific organization.
/// </summary>
public interface IMembershipRoleResolver
{
    /// <summary>
    /// Returns role names for an active membership, or an empty list otherwise.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken ct = default);
}
