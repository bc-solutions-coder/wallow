using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class TestSupportService(
    IdentityDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<TestSupportService> logger) : ITestSupportService
{
    public async Task<Guid> CreateIsolatedOrgAsync(
        Guid userId, bool requireMfa, int gracePeriodDays, CancellationToken ct = default)
    {
        string orgName = $"test-org-{Guid.NewGuid():N}";

        // The org mints its own tenant id from its org id, so the tenant to act as afterwards
        // has to be read back off the aggregate rather than chosen here.
        Organization org = Organization.Create(default, orgName, orgName, userId, timeProvider);
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync(ct);

        TenantId tenantId = org.TenantId;

        LogIsolatedOrgCreated(org.Id.Value, userId);

        dbContext.ChangeTracker.Clear();
        dbContext.SetTenant(tenantId);

        Guid adminRoleId = await ResolveAdminRoleIdAsync(ct);
        dbContext.Memberships.Add(
            Membership.Enroll(userId, org.Id, adminRoleId, timeProvider));
        await dbContext.SaveChangesAsync(ct);

        if (gracePeriodDays > 0)
        {
            WallowUser user = await dbContext.Users
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstAsync(u => u.Id == userId, ct);

            user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(gracePeriodDays));
            await dbContext.SaveChangesAsync(ct);
        }

        // Create org settings with the requested MFA policy
        OrganizationSettings settings = OrganizationSettings.Create(
            org.Id,
            tenantId,
            requireMfa,
            allowPasswordlessLogin: false,
            gracePeriodDays,
            userId,
            timeProvider);

        dbContext.OrganizationSettings.Add(settings);
        await dbContext.SaveChangesAsync(ct);

        LogIsolatedOrgConfigured(org.Id.Value, requireMfa, gracePeriodDays);

        return org.Id.Value;
    }

    private async Task<Guid> ResolveAdminRoleIdAsync(CancellationToken ct)
    {
        WallowRole? role = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == "ADMIN", ct);

        return role is null
            ? throw new InvalidOperationException("The admin role is not seeded.")
            : role.Id;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created isolated test org {OrgId} for user {UserId}")]
    private partial void LogIsolatedOrgCreated(Guid orgId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured isolated test org {OrgId}: requireMfa={RequireMfa}, gracePeriodDays={GracePeriodDays}")]
    private partial void LogIsolatedOrgConfigured(Guid orgId, bool requireMfa, int gracePeriodDays);
}
