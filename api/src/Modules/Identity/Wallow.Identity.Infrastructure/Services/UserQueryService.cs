using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
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

        Dictionary<Guid, List<string>> rolesByUserId = await RoleNamesByUserIdAsync(userIds, tenantId, ct);

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

    /// <summary>
    /// Resolves displayed role names from active memberships in the listed organization.
    /// </summary>
    private async Task<Dictionary<Guid, List<string>>> RoleNamesByUserIdAsync(
        List<Guid> userIds,
        Guid organizationId,
        CancellationToken ct)
    {
        OrganizationId scope = OrganizationId.Create(organizationId);

        // Limit membership lookup to the displayed users in this organization.
        List<Membership> memberships = await dbContext.Memberships
            .Where(m => m.OrganizationId == scope
                && m.Status == MembershipStatus.Active
                && userIds.Contains(m.UserId))
            .ToListAsync(ct);

        List<Guid> roleIds = [.. memberships.SelectMany(m => m.Roles).Select(r => r.RoleId).Distinct()];

        // Resolve assignments against the global role catalog.
        Dictionary<Guid, string> roleNamesById = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .ToDictionaryAsync(r => r.Id, r => r.Name!, ct);

        return memberships.ToDictionary(
            m => m.UserId,
            m => m.Roles
                .Select(r => roleNamesById.GetValueOrDefault(r.RoleId))
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList());
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
