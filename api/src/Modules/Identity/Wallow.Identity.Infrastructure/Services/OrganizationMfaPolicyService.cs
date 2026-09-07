using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;

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

        // Check requiring organizations across active memberships before a tenant is selected.
        // Grace is determined separately from the user's shared deadline.
        OrganizationSettings? requiring = await dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .Where(s => s.RequireMfa)
            .Where(s => dbContext.Memberships
                .IgnoreQueryFilters()
                .Any(m => m.OrganizationId == s.OrganizationId
                          && m.UserId == userId
                          && m.Status == MembershipStatus.Active))
            .FirstOrDefaultAsync(ct);

        if (requiring is null)
        {
            LogMfaNotRequired(userId);
            return new OrgMfaPolicyResult(false, false);
        }

        bool isInGracePeriod = user.MfaGraceDeadline is not null
            && user.MfaGraceDeadline > timeProvider.GetUtcNow();

        LogMfaRequired(userId, requiring.OrganizationId.Value, isInGracePeriod);
        return new OrgMfaPolicyResult(true, isInGracePeriod);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrgMfaPolicy: User {UserId} not found")]
    private partial void LogUserNotFound(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: User {UserId} already has MFA enabled")]
    private partial void LogMfaAlreadyEnabled(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: No active membership requires MFA for user {UserId}")]
    private partial void LogMfaNotRequired(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrgMfaPolicy: Org {OrgId} requires MFA for user {UserId}, inGrace={IsInGracePeriod}")]
    private partial void LogMfaRequired(Guid userId, Guid orgId, bool isInGracePeriod);
}
