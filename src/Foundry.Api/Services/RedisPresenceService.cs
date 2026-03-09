using Foundry.Shared.Contracts.Realtime;
using StackExchange.Redis;

namespace Foundry.Api.Services;

internal sealed partial class RedisPresenceService(
    IConnectionMultiplexer redis,
    ILogger<RedisPresenceService> logger) : IPresenceService
{
    private const string ConnectionPagePrefix = "presence:connpage:";
    private const string ConnectionTenantPrefix = "presence:conn:tenant:";
    private static readonly TimeSpan _connectionTtl = TimeSpan.FromMinutes(30);

    private static string ConnectionToUserKey(Guid tenantId) => $"presence:{tenantId}:conn2user";
    private static string UserConnectionsKey(Guid tenantId, string userId) => $"presence:{tenantId}:user:{userId}";
    private static string PageViewersKey(Guid tenantId, string pageContext) => $"presence:{tenantId}:page:{pageContext}";

    private IDatabase Db => redis.GetDatabase();

    public async Task TrackConnectionAsync(Guid tenantId, string userId, string connectionId, CancellationToken ct = default)
    {
        IDatabase db = Db;
        IBatch batch = db.CreateBatch();

        // Map connectionId -> userId (tenant-scoped)
        _ = batch.HashSetAsync(ConnectionToUserKey(tenantId), connectionId, userId);

        // Add connectionId to user's connection set (tenant-scoped)
        string userKey = UserConnectionsKey(tenantId, userId);
        _ = batch.SetAddAsync(userKey, connectionId);
        _ = batch.KeyExpireAsync(userKey, _connectionTtl);

        // Track which tenant this connection belongs to (global, for cleanup)
        _ = batch.StringSetAsync(ConnectionTenantPrefix + connectionId, tenantId.ToString(), _connectionTtl);

        batch.Execute();
        await Task.CompletedTask;

        LogTrackedConnection(connectionId, userId);
    }

    public async Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        IDatabase db = Db;

        // Look up which tenant this connection belongs to
        RedisValue tenantValue = await db.StringGetAsync(ConnectionTenantPrefix + connectionId);
        if (tenantValue.IsNullOrEmpty)
        {
            return;
        }

        Guid tenantId = Guid.Parse((string)tenantValue!);

        // Look up the userId for this connection
        RedisValue userId = await db.HashGetAsync(ConnectionToUserKey(tenantId), connectionId);
        if (userId.IsNullOrEmpty)
        {
            return;
        }

        string userIdStr = userId!;
        IBatch batch = db.CreateBatch();

        // Remove from tenant-scoped conn2user
        _ = batch.HashDeleteAsync(ConnectionToUserKey(tenantId), connectionId);

        // Remove from tenant-scoped user connection set
        _ = batch.SetRemoveAsync(UserConnectionsKey(tenantId, userIdStr), connectionId);

        // Remove page context (global key)
        RedisValue pageContext = await db.StringGetAsync(ConnectionPagePrefix + connectionId);
        _ = batch.KeyDeleteAsync(ConnectionPagePrefix + connectionId);

        if (!pageContext.IsNullOrEmpty)
        {
            _ = batch.SetRemoveAsync(PageViewersKey(tenantId, pageContext!), connectionId);
        }

        // Remove tenant tracking key
        _ = batch.KeyDeleteAsync(ConnectionTenantPrefix + connectionId);

        batch.Execute();

        LogRemovedConnection(connectionId, userIdStr);
    }

    public async Task SetPageContextAsync(Guid tenantId, string connectionId, string pageContext, CancellationToken ct = default)
    {
        IDatabase db = Db;

        // Remove from old page if any
        RedisValue oldPage = await db.StringGetAsync(ConnectionPagePrefix + connectionId);
        if (!oldPage.IsNullOrEmpty)
        {
            await db.SetRemoveAsync(PageViewersKey(tenantId, oldPage!), connectionId);
        }

        IBatch batch = db.CreateBatch();

        // Set new page context (global key — connection IDs are unique)
        _ = batch.StringSetAsync(ConnectionPagePrefix + connectionId, pageContext, _connectionTtl);

        // Add to tenant-scoped page viewers set
        string pageKey = PageViewersKey(tenantId, pageContext);
        _ = batch.SetAddAsync(pageKey, connectionId);
        _ = batch.KeyExpireAsync(pageKey, _connectionTtl);

        batch.Execute();
    }

    public async Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        IDatabase db = Db;
        HashEntry[] allEntries = await db.HashGetAllAsync(ConnectionToUserKey(tenantId));

        // Group connections by userId
        Dictionary<string, List<string>> userConnections = [];
        foreach (HashEntry entry in allEntries)
        {
            string connId = entry.Name!;
            string userId = entry.Value!;
            if (!userConnections.TryGetValue(userId, out List<string>? list))
            {
                list = [];
                userConnections[userId] = list;
            }
            list.Add(connId);
        }

        List<UserPresence> result = [];
        foreach ((string userId, List<string> connectionIds) in userConnections)
        {
            List<string> pages = [];
            foreach (string connId in connectionIds)
            {
                RedisValue page = await db.StringGetAsync(ConnectionPagePrefix + connId);
                if (!page.IsNullOrEmpty)
                {
                    pages.Add(page!);
                }
            }

            result.Add(new UserPresence(userId, null, connectionIds, pages.Distinct().ToList()));
        }

        return result;
    }

    public async Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(Guid tenantId, string pageContext, CancellationToken ct = default)
    {
        IDatabase db = Db;
        RedisValue[] connectionIds = await db.SetMembersAsync(PageViewersKey(tenantId, pageContext));

        Dictionary<string, List<string>> userConnections = [];
        foreach (RedisValue connId in connectionIds)
        {
            RedisValue userId = await db.HashGetAsync(ConnectionToUserKey(tenantId), connId!);
            if (userId.IsNullOrEmpty)
            {
                continue;
            }

            string uid = userId!;
            if (!userConnections.TryGetValue(uid, out List<string>? list))
            {
                list = [];
                userConnections[uid] = list;
            }
            list.Add(connId!);
        }

        return userConnections
            .Select(kvp => new UserPresence(kvp.Key, null, kvp.Value, [pageContext]))
            .ToList();
    }

    public async Task<bool> IsUserOnlineAsync(Guid tenantId, string userId, CancellationToken ct = default)
    {
        IDatabase db = Db;
        long length = await db.SetLengthAsync(UserConnectionsKey(tenantId, userId));
        return length > 0;
    }

    public async Task<string?> GetUserIdByConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        IDatabase db = Db;

        // Look up tenant first, then get userId from tenant-scoped key
        RedisValue tenantValue = await db.StringGetAsync(ConnectionTenantPrefix + connectionId);
        if (tenantValue.IsNullOrEmpty)
        {
            return null;
        }

        Guid tenantId = Guid.Parse((string)tenantValue!);
        RedisValue userId = await db.HashGetAsync(ConnectionToUserKey(tenantId), connectionId);
        return userId.IsNullOrEmpty ? null : (string)userId!;
    }
}

internal sealed partial class RedisPresenceService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Tracked connection {ConnectionId} for user {UserId}")]
    private partial void LogTrackedConnection(string connectionId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed connection {ConnectionId} for user {UserId}")]
    private partial void LogRemovedConnection(string connectionId, string userId);
}
