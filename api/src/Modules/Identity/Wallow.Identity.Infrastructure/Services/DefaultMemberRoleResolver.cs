using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Public because Wolverine's generated handlers construct their dependencies inline and
/// <c>ServiceLocationPolicy.NotAllowed</c> turns a non-public concrete type into a codegen
/// failure at the first message.
/// </summary>
public sealed class DefaultMemberRoleResolver(IdentityDbContext dbContext) : IDefaultMemberRoleResolver
{
    /// <summary>
    /// The role a member holds when their organization has configured no default. It is the
    /// baseline the platform ships, deliberately the least privileged one.
    /// </summary>
    private const string BaselineMemberRoleName = "user";

    /// <summary>Spelled normalized because the lookup is by <c>NormalizedName</c> in a query EF
    /// translates — a <c>ToUpperInvariant</c> call inside the predicate is not translatable.</summary>
    private const string BaselineMemberRoleNormalizedName = "USER";

    public async Task<Guid> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // The settings row is keyed globally on organization_id and this runs at authorize time,
        // before any tenant is resolved, so the filter would hide the only row that matters.
        Guid? configured = await dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == orgId)
            .Select(s => s.DefaultRoleId)
            .FirstOrDefaultAsync(ct);

        if (configured is not null && await RoleExistsAsync(configured.Value, ct))
        {
            return configured.Value;
        }

        WallowRole? baseline = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == BaselineMemberRoleNormalizedName, ct);

        if (baseline is null)
        {
            throw new BusinessRuleException(
                "Identity.RoleNotFound",
                $"Role '{BaselineMemberRoleName}' does not exist");
        }

        return baseline.Id;
    }

    /// <summary>
    /// A configured role can be deleted after it was chosen. Falling back beats admitting nobody:
    /// the organization keeps working and the member lands on the least privileged role.
    /// </summary>
    private Task<bool> RoleExistsAsync(Guid roleId, CancellationToken ct) =>
        dbContext.Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == roleId, ct);
}
