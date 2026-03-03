using System.Net.Http.Json;
using Foundry.Shared.Contracts.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class UserQueryService : IUserQueryService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserQueryService> _logger;
    private readonly string _realm;
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(60);

    public UserQueryService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<UserQueryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _cache = cache;
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
    }

    public async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        string cacheKey = $"user-email:{userId}";
        try
        {
            string email = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{userId}", ct);
                if (!response.IsSuccessStatusCode)
                {
                    LogGetUserEmailFailed(userId);
                    return string.Empty;
                }

                UserEmailRepresentation? user = await response.Content.ReadFromJsonAsync<UserEmailRepresentation>(ct);
                return user?.Email ?? string.Empty;
            }) ?? string.Empty;

            return email;
        }
        catch (Exception ex)
        {
            LogGetUserEmailException(ex, userId);
            return string.Empty;
        }
    }

    public async Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        string cacheKey = $"new-users-count:{tenantId}:{from:O}:{to:O}";
        try
        {
            int count = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                List<UserMemberRepresentation>? members = await GetOrganizationMembersAsync(tenantId, ct);
                if (members == null || members.Count == 0)
                {
                    return 0;
                }

                long fromTimestamp = new DateTimeOffset(from).ToUnixTimeMilliseconds();
                long toTimestamp = new DateTimeOffset(to).ToUnixTimeMilliseconds();

                return members.Count(m =>
                    m.CreatedTimestamp >= fromTimestamp &&
                    m.CreatedTimestamp.Value < toTimestamp);
            });

            LogNewUsersCount(count, tenantId, from, to);
            return count;
        }
        catch (Exception ex)
        {
            LogGetNewUsersCountFailed(ex, tenantId);
            return 0;
        }
    }

    public async Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        string cacheKey = $"active-users-count:{tenantId}";
        try
        {
            int count = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                List<UserMemberRepresentation>? members = await GetOrganizationMembersAsync(tenantId, ct);
                if (members == null || members.Count == 0)
                {
                    return 0;
                }

                long thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

                return members.Count(m =>
                    m.Enabled == true &&
                    (!m.CreatedTimestamp.HasValue || m.CreatedTimestamp.Value >= thirtyDaysAgo));
            });

            LogActiveUsersCount(count, tenantId);
            return count;
        }
        catch (Exception ex)
        {
            LogGetActiveUsersCountFailed(ex, tenantId);
            return 0;
        }
    }

    public async Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        string cacheKey = $"total-users-count:{tenantId}";
        try
        {
            int count = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                List<UserMemberRepresentation>? members = await GetOrganizationMembersAsync(tenantId, ct);
                return members?.Count ?? 0;
            });

            LogTotalUsersCount(count, tenantId);
            return count;
        }
        catch (Exception ex)
        {
            LogGetTotalUsersCountFailed(ex, tenantId);
            return 0;
        }
    }

    private async Task<List<UserMemberRepresentation>?> GetOrganizationMembersAsync(Guid orgId, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/organizations/{orgId}/members", ct);

        if (!response.IsSuccessStatusCode)
        {
            LogFetchMembersFailed(orgId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<UserMemberRepresentation>>(ct);
    }

    private sealed record UserEmailRepresentation
    {
        public string? Email { get; init; }
    }

    private sealed record UserMemberRepresentation
    {
        public string? Id { get; init; }
        public string? Email { get; init; }
        public bool? Enabled { get; init; }
        public long? CreatedTimestamp { get; init; }
    }
}

public sealed partial class UserQueryService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get email for user {UserId}")]
    private partial void LogGetUserEmailFailed(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exception getting email for user {UserId}")]
    private partial void LogGetUserEmailException(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} new users for organization {OrgId} between {From} and {To}")]
    private partial void LogNewUsersCount(int count, Guid orgId, DateTime from, DateTime to);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get new users count for organization {OrgId}")]
    private partial void LogGetNewUsersCountFailed(Exception ex, Guid orgId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} active users for organization {OrgId}")]
    private partial void LogActiveUsersCount(int count, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get active users count for organization {OrgId}")]
    private partial void LogGetActiveUsersCountFailed(Exception ex, Guid orgId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} total users for organization {OrgId}")]
    private partial void LogTotalUsersCount(int count, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get total users count for organization {OrgId}")]
    private partial void LogGetTotalUsersCountFailed(Exception ex, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch members for organization {OrgId}: {StatusCode}")]
    private partial void LogFetchMembersFailed(Guid orgId, System.Net.HttpStatusCode statusCode);
}
