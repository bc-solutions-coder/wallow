using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class UserQueryService(
    UserManager<WallowUser> userManager,
    IdentityDbContext dbContext,
    ILogger<UserQueryService> logger) : IUserQueryService
{
    public async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            LogGetUserEmailFailed(userId);
            return string.Empty;
        }

        return user.Email ?? string.Empty;
    }

    public async Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        DateTimeOffset fromOffset = new(from, TimeSpan.Zero);
        DateTimeOffset toOffset = new(to, TimeSpan.Zero);

        int count = await dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .Where(u => u.CreatedAt >= fromOffset && u.CreatedAt < toOffset)
            .CountAsync(ct);

        LogNewUsersCount(count, tenantId, from, to);
        return count;
    }

    public async Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        int count = await dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .Where(u => u.IsActive)
            .CountAsync(ct);

        LogActiveUsersCount(count, tenantId);
        return count;
    }

    public async Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        int count = await dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .CountAsync(ct);

        LogTotalUsersCount(count, tenantId);
        return count;
    }
}

public sealed partial class UserQueryService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get email for user {UserId}")]
    private partial void LogGetUserEmailFailed(Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} new users for organization {OrgId} between {From} and {To}")]
    private partial void LogNewUsersCount(int count, Guid orgId, DateTime from, DateTime to);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} active users for organization {OrgId}")]
    private partial void LogActiveUsersCount(int count, Guid orgId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} total users for organization {OrgId}")]
    private partial void LogTotalUsersCount(int count, Guid orgId);
}
