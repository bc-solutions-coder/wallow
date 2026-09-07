using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class DefaultMemberRoleResolver(IdentityDbContext dbContext) : IDefaultMemberRoleResolver
{
    /// <summary>
    /// Fallback role when no existing configured default is available.
    /// </summary>
    private const string BaselineMemberRoleName = "user";

    /// <summary>
    /// Normalized name used directly in the database predicate.
    /// </summary>
    private const string BaselineMemberRoleNormalizedName = "USER";

    public async Task<Guid> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // Enrollment may run without a tenant; select settings by organization explicitly.
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
            throw new BusinessRuleException(IdentityErrors.RoleNotFound, $"Role '{BaselineMemberRoleName}' does not exist");
        }

        return baseline.Id;
    }

    /// <summary>
    /// A deleted configured role falls back to the baseline role.
    /// </summary>
    private Task<bool> RoleExistsAsync(Guid roleId, CancellationToken ct) =>
        dbContext.Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == roleId, ct);
}
