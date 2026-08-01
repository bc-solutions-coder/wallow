namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The replacement for <c>userManager.GetRolesAsync(user)</c> in every authorization context:
/// roles are granted by an organization, so they only ever resolve against one.
/// </summary>
public interface IMembershipRoleResolver
{
    /// <summary>
    /// The role names granted to this user BY this organization. Empty when there is no
    /// membership, or the membership is not Active. Feeds RolePermissionMapping unchanged.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken ct = default);
}
