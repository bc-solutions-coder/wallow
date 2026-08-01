using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Resolves organization ownership from the caller's membership of that one organization. The
/// membership reads run before any tenant is resolved and constrain only on their own parameters,
/// which is what makes a creator's just-created org resolvable here.
/// </summary>
public sealed class OrganizationAccessPolicy(
    IMembershipRoleResolver roleResolver) : IOrganizationAccessPolicy
{
    private const string AdminRoleName = "admin";

    public async Task<bool> IsOrganizationAdminAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        IReadOnlyList<string> roleNames = await roleResolver.GetRoleNamesAsync(userId, organizationId, ct);

        return roleNames.Contains(AdminRoleName, StringComparer.OrdinalIgnoreCase);
    }
}
