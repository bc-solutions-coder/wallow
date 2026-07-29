using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Resolves organization ownership from the membership roster. The repository reads organizations
/// with <c>IgnoreQueryFilters</c> (an org IS the tenant, so the ambient filter would hide every org
/// but the caller's own), which is what makes a creator's just-created org resolvable here.
/// </summary>
public sealed class OrganizationAccessPolicy(IOrganizationRepository organizationRepository) : IOrganizationAccessPolicy
{
    public async Task<bool> IsOrganizationAdminAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        Organization? organization = await organizationRepository.GetByIdAsync(
            OrganizationId.Create(organizationId), ct);

        return organization is not null
            && organization.Members.Any(m => m.UserId == userId && m.Role == OrgMemberRole.Admin);
    }
}
