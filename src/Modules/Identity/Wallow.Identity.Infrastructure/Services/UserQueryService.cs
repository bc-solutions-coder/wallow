using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity;

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
            .Where(u => u.CreatedAt >= fromOffset && u.CreatedAt < toOffset)
            .CountAsync(ct);

        LogNewUsersCount(count, tenantId, from, to);
        return count;
    }

    public async Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        int count = await dbContext.Users
            .Where(u => u.IsActive)
            .CountAsync(ct);

        LogActiveUsersCount(count, tenantId);
        return count;
    }

    public async Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        int count = await dbContext.Users
            .CountAsync(ct);

        LogTotalUsersCount(count, tenantId);
        return count;
    }

    public async Task<UserSearchPageResult> SearchUsersAsync(Guid tenantId, string? search, int skip, int take, CancellationToken ct = default)
    {
        IQueryable<WallowUser> query = dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Email!, pattern) ||
                EF.Functions.ILike(u.FirstName!, pattern) ||
                EF.Functions.ILike(u.LastName!, pattern));
        }

        int totalCount = await query.CountAsync(ct);

        List<WallowUser> users = await query
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        List<Guid> userIds = users.Select(u => u.Id).ToList();

        // Batch role lookup: join UserRoles with Roles, group by UserId
        Dictionary<Guid, List<string>> rolesByUserId = await dbContext.Set<IdentityUserRole<Guid>>()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(
                dbContext.Set<WallowRole>(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, RoleName = r.Name! })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(x => x.RoleName).ToList(),
                ct);

        List<UserSearchItem> items = users.Select(u => new UserSearchItem(
            u.Id,
            u.Email ?? string.Empty,
            u.FirstName ?? string.Empty,
            u.LastName ?? string.Empty,
            u.IsActive,
            rolesByUserId.GetValueOrDefault(u.Id, []))).ToList();

        int page = take > 0 ? (skip / take) + 1 : 1;

        return new UserSearchPageResult(items, totalCount, page, take);
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
