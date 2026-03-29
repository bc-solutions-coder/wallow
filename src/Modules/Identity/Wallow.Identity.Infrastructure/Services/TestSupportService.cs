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
        TenantId tenantId = new(Guid.NewGuid());

        // Create the isolated org with its own tenant
        Organization org = Organization.Create(tenantId, orgName, orgName, userId, timeProvider);
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync(ct);

        LogIsolatedOrgCreated(org.Id.Value, userId);

        // Add the user as a member of the new org
        dbContext.ChangeTracker.Clear();
        dbContext.SetTenant(tenantId);

        Organization trackedOrg = await dbContext.Organizations
            .AsTracking()
            .Include(o => o.Members)
            .FirstAsync(o => o.Id == org.Id, ct);

        trackedOrg.AddMember(userId, "admin", userId, timeProvider);
        await dbContext.SaveChangesAsync(ct);

        // Move the user's TenantId to the new org (keep user in shared org for OIDC tenant resolution)
        WallowUser user = await dbContext.Users
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstAsync(u => u.Id == userId, ct);

        user.TenantId = tenantId.Value;

        if (gracePeriodDays > 0)
        {
            user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(gracePeriodDays));
        }

        await dbContext.SaveChangesAsync(ct);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Created isolated test org {OrgId} for user {UserId}")]
    private partial void LogIsolatedOrgCreated(Guid orgId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured isolated test org {OrgId}: requireMfa={RequireMfa}, gracePeriodDays={GracePeriodDays}")]
    private partial void LogIsolatedOrgConfigured(Guid orgId, bool requireMfa, int gracePeriodDays);
}
