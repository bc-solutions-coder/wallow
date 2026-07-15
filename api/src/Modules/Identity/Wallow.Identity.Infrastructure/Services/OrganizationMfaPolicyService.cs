using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OrganizationMfaPolicyService(
    IdentityDbContext dbContext,
    UserManager<WallowUser> userManager,
    TimeProvider timeProvider,
    ILogger<OrganizationMfaPolicyService> logger) : IOrganizationMfaPolicyService
{
    public async Task<OrgMfaPolicyResult> CheckAsync(Guid userId, CancellationToken ct)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            LogUserNotFound(userId);
            return new OrgMfaPolicyResult(false, false);
        }

        if (user.MfaEnabled)
        {
            LogMfaAlreadyEnabled(userId);
            return new OrgMfaPolicyResult(false, false);
        }

        // IgnoreQueryFilters: this runs during login when tenant context is not yet set.
        // Prefer the org matching the user's TenantId (the user may be in multiple orgs).
        Organization? organization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .Include(o => o.Members)
            .FirstOrDefaultAsync(
                o => o.TenantId == new TenantId(user.TenantId)
                     && o.Members.Any(m => m.UserId == userId), ct);

        if (organization is null)
        {
            LogNoOrganization(userId);
            return new OrgMfaPolicyResult(false, false);
        }

        OrganizationSettings? settings = await dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == organization.Id, ct);

        if (settings is null)
        {
            LogNoSettings(userId, organization.Id.Value);
            return new OrgMfaPolicyResult(false, false);
        }

        if (!settings.RequireMfa)
        {
            LogMfaNotRequired(userId, organization.Id.Value);
            return new OrgMfaPolicyResult(false, false);
        }

        // Org requires MFA and user hasn't enrolled — check grace period
        bool isInGracePeriod = user.MfaGraceDeadline is not null
            && user.MfaGraceDeadline > timeProvider.GetUtcNow();

        LogMfaRequired(userId, organization.Id.Value, isInGracePeriod);
        return new OrgMfaPolicyResult(true, isInGracePeriod);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrgMfaPolicy: User {UserId} not found")]
    private partial void LogUserNotFound(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: User {UserId} already has MFA enabled")]
    private partial void LogMfaAlreadyEnabled(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrgMfaPolicy: User {UserId} not in any organization")]
    private partial void LogNoOrganization(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrgMfaPolicy: No settings for user {UserId} org {OrgId}")]
    private partial void LogNoSettings(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: Org {OrgId} does not require MFA for user {UserId}")]
    private partial void LogMfaNotRequired(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: Org {OrgId} requires MFA for user {UserId}, inGrace={IsInGracePeriod}")]
    private partial void LogMfaRequired(Guid userId, Guid orgId, bool isInGracePeriod);
}
