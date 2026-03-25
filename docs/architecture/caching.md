# Caching Guide

This guide covers caching patterns in Wallow using Valkey (a Redis-compatible cache).

## Overview

Wallow uses **Valkey** as its distributed caching and real-time infrastructure layer. Valkey is a Redis-compatible, open-source key-value store that serves multiple purposes in the platform:

| Use Case | Description |
|----------|-------------|
| **Distributed Cache** | `IDistributedCache` for cross-instance data sharing |
| **SignalR Backplane** | WebSocket message distribution across server instances |
| **Presence Tracking** | Real-time user online status and page context |
| **API Key Storage** | Service account authentication tokens |
| **Metering Counters** | High-performance usage tracking and quota enforcement |

## Configuration

### Connection String

Configure the Valkey connection in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

Common connection options:

| Option | Description | Example |
|--------|-------------|---------|
| `abortConnect` | Don't fail on startup if Redis is unavailable | `false` |
| `connectTimeout` | Connection timeout in ms | `5000` |
| `syncTimeout` | Sync operation timeout in ms | `5000` |
| `password` | Authentication password | `your-password` |
| `ssl` | Enable TLS | `true` |
| `allowAdmin` | Enable admin commands (FLUSHDB, etc.) | `true` (tests only) |

Production example:

```
redis.example.com:6380,password=secret,ssl=true,abortConnect=false
```

### Docker Container (Local Development)

The `docker-compose.yml` includes a Valkey container:

```yaml
valkey:
  image: valkey/valkey:8-alpine
  container_name: ${COMPOSE_PROJECT_NAME:-wallow}-valkey
  command: valkey-server --appendonly yes --requirepass ${VALKEY_PASSWORD}
  ports:
    - "127.0.0.1:6379:6379"
  volumes:
    - valkey_data:/data
  environment:
    REDISCLI_AUTH: ${VALKEY_PASSWORD}
  healthcheck:
    test: ["CMD", "valkey-cli", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
```

Start the infrastructure:

```bash
cd docker && docker compose up -d
```

### StackExchange.Redis Registration

In `Program.cs`, the connection is registered as a singleton with deferred connection:

```csharp
// Connection multiplexer - deferred to allow WebApplicationFactory overrides
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string not configured");
    return ConnectionMultiplexer.Connect(connectionString);
});
```

## Distributed Caching

### IDistributedCache Usage

For standard caching scenarios, use the built-in `IDistributedCache` interface:

```csharp
// Registration in Program.cs — reuses the singleton IConnectionMultiplexer
builder.Services.AddStackExchangeRedisCache(_ => { });
builder.Services.AddSingleton<IConfigureOptions<RedisCacheOptions>>(sp =>
{
    IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
    return new ConfigureNamedOptions<RedisCacheOptions>(
        Options.DefaultName,
        options => options.ConnectionMultiplexerFactory = () => Task.FromResult(mux));
});

// Wrap with instrumented decorator for cache hit/miss metrics
builder.Services.AddSingleton<IDistributedCache>(sp =>
{
    IOptions<RedisCacheOptions> options = sp.GetRequiredService<IOptions<RedisCacheOptions>>();
    RedisCache inner = new(options);
    return new InstrumentedDistributedCache(inner);
});
```

### Cache Key Patterns

Follow consistent key naming conventions:

| Pattern | Example | Use Case |
|---------|---------|----------|
| `{domain}:{identifier}` | `feature-flag:dark-mode` | Simple lookups |
| `{domain}:{tenant}:{identifier}` | `meter:abc-123:api.calls:2026-02` | Tenant-scoped data |
| `{domain}:{scope}:{key}` | `presence:user:user-id-123` | Hierarchical data |

### Serialization

Use System.Text.Json for cache serialization:

```csharp
using System.Text.Json;

public class FeatureFlagCache : IFeatureFlagCache
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<FeatureFlag?> GetFlagAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = $"feature-flag:{key}";
        var data = await _cache.GetStringAsync(cacheKey, ct);

        if (data is null)
            return null;

        return JsonSerializer.Deserialize<FeatureFlag>(data);
    }

    public async Task SetFlagAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        var cacheKey = $"feature-flag:{flag.Key}";
        var data = JsonSerializer.Serialize(flag);

        await _cache.SetStringAsync(cacheKey, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        }, ct);
    }

    public async Task InvalidateFlagAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = $"feature-flag:{key}";
        await _cache.RemoveAsync(cacheKey, ct);
    }
}
```

### TTL Strategies

| Strategy | Use Case | Example |
|----------|----------|---------|
| **Absolute Expiration** | Data that becomes stale | Feature flags (5 min) |
| **Sliding Expiration** | Session-like data | User preferences (30 min) |
| **No Expiration + Manual Invalidation** | Rarely changing data | Configuration |
| **Time-Based Keys** | Period-scoped counters | `meter:tenant:api.calls:2026-02` |

```csharp
// Absolute expiration - expires exactly 5 minutes after set
new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
}

// Sliding expiration - resets on each access
new DistributedCacheEntryOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(30)
}

// Combined - sliding with absolute cap
new DistributedCacheEntryOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(10),
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
}
```

## Caching Patterns

### Cache-Aside Pattern

The most common pattern in Wallow. Check cache first, fall back to source on miss:

```csharp
public async Task<FeatureFlag?> GetFlagAsync(string key, CancellationToken ct)
{
    // 1. Check cache
    var cached = await _cache.GetFlagAsync(key, ct);
    if (cached is not null)
        return cached;

    // 2. Cache miss - load from database
    var flag = await _repository.GetByKeyAsync(key, ct);
    if (flag is null)
        return null;

    // 3. Populate cache
    await _cache.SetFlagAsync(flag, ct);

    return flag;
}
```

### Direct Redis Access for High-Performance Operations

For counters and complex data structures, use `IConnectionMultiplexer` directly:

```csharp
public sealed class ValkeyMeteringService : IMeteringService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITenantContext _tenantContext;

    public async Task IncrementAsync(string meterCode, decimal value = 1)
    {
        var tenantId = _tenantContext.TenantId;
        var period = DateTime.UtcNow.ToString("yyyy-MM");
        var key = $"meter:{tenantId.Value}:{meterCode}:{period}";

        var db = _redis.GetDatabase();
        await db.StringIncrementAsync(key, (long)value);

        // Set expiry to prevent unbounded growth
        await db.KeyExpireAsync(key, TimeSpan.FromDays(90), ExpireWhen.HasNoExpiry);
    }

    public async Task<QuotaCheckResult> CheckQuotaAsync(string meterCode)
    {
        var db = _redis.GetDatabase();
        var key = $"meter:{_tenantContext.TenantId.Value}:{meterCode}:{period}";

        var currentValue = (long?)await db.StringGetAsync(key) ?? 0;
        // ... quota logic
    }
}
```

### Batched Operations

Use Redis batching for related operations:

```csharp
public async Task TrackConnectionAsync(string userId, string connectionId)
{
    var db = _redis.GetDatabase();
    var batch = db.CreateBatch();

    // Map connectionId -> userId
    _ = batch.HashSetAsync("presence:conn2user", connectionId, userId);

    // Add connectionId to user's connection set
    var userKey = $"presence:user:{userId}";
    _ = batch.SetAddAsync(userKey, connectionId);
    _ = batch.KeyExpireAsync(userKey, TimeSpan.FromMinutes(30));

    batch.Execute();
}
```

### Cache Invalidation Strategies

**Event-Driven Invalidation** - Invalidate when data changes:

```csharp
public class FeatureFlagUpdatedConsumer
{
    private readonly IFeatureFlagCache _cache;

    public async Task Handle(FeatureFlagUpdatedEvent @event, CancellationToken ct)
    {
        await _cache.InvalidateFlagAsync(@event.Key, ct);
    }
}
```

**Time-Based Expiration** - Let entries expire naturally:

```csharp
// Good for frequently-read, slowly-changing data
await _cache.SetStringAsync(key, data, new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
}, ct);
```

**Write-Through Invalidation** - Clear cache on write:

```csharp
public async Task UpdateFlagAsync(FeatureFlag flag, CancellationToken ct)
{
    // Write to database
    await _repository.UpdateAsync(flag, ct);

    // Immediately invalidate cache
    await _cache.InvalidateFlagAsync(flag.Key, ct);
}
```

### Tenant-Scoped Caching

Always include tenant ID in cache keys for multi-tenant data:

```csharp
public async Task<T?> GetTenantDataAsync<T>(string key, CancellationToken ct)
{
    var tenantId = _tenantContext.TenantId;
    var cacheKey = $"tenant:{tenantId.Value}:{key}";

    var data = await _cache.GetStringAsync(cacheKey, ct);
    return data is null ? default : JsonSerializer.Deserialize<T>(data);
}
```

## SignalR Backplane

Valkey serves as the SignalR backplane for horizontal scaling. When multiple API instances run behind a load balancer, the backplane ensures WebSocket messages reach all connected clients regardless of which instance they're connected to.

### Configuration

```csharp
// SignalR with Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Wallow");
        options.ConnectionFactory = async writer =>
        {
            var connStr = configRef.GetConnectionString("Redis")!;
            return await ConnectionMultiplexer.ConnectAsync(connStr, writer);
        };
    });
```

### How It Works

1. Client connects to Instance A
2. Instance B publishes a message for that client
3. Redis pub/sub forwards the message to Instance A
4. Instance A delivers to the connected client

The backplane uses Redis pub/sub channels with the configured prefix (`Wallow`).

## Presence Tracking

The `RedisPresenceService` tracks online users and their current page context:

```csharp
public sealed class RedisPresenceService : IPresenceService
{
    private const string ConnectionToUserKey = "presence:conn2user";
    private const string UserConnectionsPrefix = "presence:user:";
    private const string ConnectionPagePrefix = "presence:connpage:";
    private const string PageViewersPrefix = "presence:page:";
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromMinutes(30);

    public async Task<bool> IsUserOnlineAsync(string userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var length = await db.SetLengthAsync(UserConnectionsPrefix + userId);
        return length > 0;
    }

    public async Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(
        string pageContext, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var connectionIds = await db.SetMembersAsync(PageViewersPrefix + pageContext);
        // Map connection IDs to user presence data
    }
}
```

### Key Structure

| Key Pattern | Type | Purpose |
|-------------|------|---------|
| `presence:conn2user` | Hash | Maps connectionId -> userId |
| `presence:user:{userId}` | Set | All connectionIds for a user |
| `presence:connpage:{connId}` | String | Current page for a connection |
| `presence:page:{context}` | Set | All connectionIds viewing a page |

All presence keys use TTLs (30 minutes) as a safety net against orphaned entries.

## API Key Storage

Service account API keys are stored in Redis for fast validation:

```csharp
public sealed class RedisApiKeyService : IApiKeyService
{
    private const string KeyPrefix = "apikey:";
    private const string UserKeysPrefix = "apikeys:user:";

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(
        string apiKey, CancellationToken ct)
    {
        var keyHash = HashApiKey(apiKey);
        var db = _redis.GetDatabase();

        var json = await db.StringGetAsync($"{KeyPrefix}{keyHash}");
        if (json.IsNullOrEmpty)
            return ApiKeyValidationResult.NotFound();

        var data = JsonSerializer.Deserialize<ApiKeyData>(json.ToString());
        // Validate expiration, return result
    }
}
```

### Key Structure

| Key Pattern | Content | Expiry |
|-------------|---------|--------|
| `apikey:{hash}` | Full API key metadata | Optional (key expiration) |
| `apikey:id:{keyId}` | Same metadata by ID | Same as above |
| `apikeys:user:{userId}` | Set of keyIds | None |

API keys are hashed before storage - the raw key is never persisted.

## Best Practices

### Key Naming Conventions

1. **Use colons as separators**: `domain:scope:identifier`
2. **Include tenant ID for multi-tenant data**: `tenant:{tenantId}:resource:{id}`
3. **Use time-based suffixes for period-scoped data**: `meter:{tenant}:api.calls:2026-02`
4. **Keep keys readable**: Avoid abbreviations, use clear names

### Avoiding Cache Stampede

When a popular cache key expires, multiple requests may simultaneously try to rebuild it. Strategies:

**Lock-Based Refresh**:
```csharp
public async Task<T?> GetWithLockAsync<T>(string key, Func<Task<T?>> factory)
{
    var cached = await _cache.GetStringAsync(key);
    if (cached is not null)
        return JsonSerializer.Deserialize<T>(cached);

    var lockKey = $"lock:{key}";
    var db = _redis.GetDatabase();

    // Try to acquire lock
    if (await db.StringSetAsync(lockKey, "1", TimeSpan.FromSeconds(30), When.NotExists))
    {
        try
        {
            var value = await factory();
            if (value is not null)
            {
                await _cache.SetStringAsync(key, JsonSerializer.Serialize(value));
            }
            return value;
        }
        finally
        {
            await db.KeyDeleteAsync(lockKey);
        }
    }

    // Lock held by another request - wait and retry
    await Task.Delay(100);
    return await GetWithLockAsync(key, factory);
}
```

**Staggered Expiration**:
```csharp
// Add jitter to prevent synchronized expiration
var ttl = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(Random.Shared.Next(0, 60));
```

### Handling Cache Failures Gracefully

Cache should enhance performance, not be a hard dependency:

```csharp
public async Task<FeatureFlag?> GetFlagWithFallbackAsync(string key, CancellationToken ct)
{
    try
    {
        var cached = await _cache.GetFlagAsync(key, ct);
        if (cached is not null)
            return cached;
    }
    catch (RedisConnectionException ex)
    {
        _logger.LogWarning(ex, "Cache unavailable, falling back to database");
        // Continue to database fallback
    }

    return await _repository.GetByKeyAsync(key, ct);
}
```

### Cache Warming

Pre-populate cache on startup for critical data:

```csharp
public class CacheWarmingBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Warm frequently-accessed data
        var flags = await _repository.GetAllActiveAsync(stoppingToken);
        foreach (var flag in flags)
        {
            await _cache.SetFlagAsync(flag, stoppingToken);
        }

        _logger.LogInformation("Warmed {Count} feature flags into cache", flags.Count);
    }
}
```

## Testing with Cache

### Mocking IDistributedCache

For unit tests, mock the cache interface:

```csharp
public class FeatureFlagCacheTests
{
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly FeatureFlagCache _sut;

    public FeatureFlagCacheTests()
    {
        _sut = new FeatureFlagCache(_cache);
    }

    [Fact]
    public async Task GetFlagAsync_CacheHit_ReturnsFlag()
    {
        var flag = new FeatureFlag { Key = "test-flag", IsEnabled = true };
        var json = JsonSerializer.Serialize(flag);

        _cache.GetStringAsync("feature-flag:test-flag", Arg.Any<CancellationToken>())
            .Returns(json);

        var result = await _sut.GetFlagAsync("test-flag");

        result.Should().NotBeNull();
        result!.Key.Should().Be("test-flag");
    }
}
```

### Mocking IConnectionMultiplexer

For direct Redis operations:

```csharp
public class ValkeyMeteringServiceTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();

    public ValkeyMeteringServiceTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
    }

    [Fact]
    public async Task IncrementAsync_ShouldIncrementCounter()
    {
        var service = new ValkeyMeteringService(_redis, /* other deps */);

        await service.IncrementAsync("api.calls", 1);

        await _database.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("api.calls")),
            1,
            Arg.Any<CommandFlags>());
    }
}
```

### Integration Tests with Real Valkey

Use Testcontainers for integration tests:

```csharp
public class RedisPresenceServiceTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .Build();

    private IConnectionMultiplexer _multiplexer = null!;
    private RedisPresenceService _sut = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _sut = new RedisPresenceService(_multiplexer, NullLogger<RedisPresenceService>.Instance);
    }

    public async Task DisposeAsync()
    {
        _multiplexer.Dispose();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TrackConnection_ShouldMakeUserOnline()
    {
        await _sut.TrackConnectionAsync("user-1", "conn-1");

        var isOnline = await _sut.IsUserOnlineAsync("user-1");
        isOnline.Should().BeTrue();
    }
}
```

### Shared Redis Fixture

For multiple test classes sharing a container:

```csharp
public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .Build();

    public string ConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync() => await _redis.StartAsync();
    public async Task DisposeAsync() => await _redis.DisposeAsync();
}

[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisFixture> { }

[Collection("Redis")]
public class MyTests
{
    private readonly RedisFixture _redis;

    public MyTests(RedisFixture redis)
    {
        _redis = redis;
    }
}
```

### Cleaning State Between Tests

```csharp
public class MeteringIntegrationTestBase : IAsyncLifetime
{
    protected IConnectionMultiplexer Redis = null!;

    public virtual async Task InitializeAsync()
    {
        Redis = Factory.Services.GetRequiredService<IConnectionMultiplexer>();

        // Clean up Valkey state before each test
        var server = Redis.GetServer(Redis.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }
}
```

Note: `FlushDatabaseAsync` requires `allowAdmin=true` in the connection string, which the test factory configures automatically.

## Health Checks

Valkey health is monitored via ASP.NET Core health checks:

```csharp
services.AddHealthChecks()
    .AddRedis(
        configuration.GetConnectionString("Redis")!,
        name: "redis",
        tags: ["infrastructure", "ready"]);
```

Check status at `/health/ready`.

## Related Documentation

- [SignalR Real-Time Design](/docs/plans/2026-02-04-signalr-realtime-design.md)
- [Testing Guide](/docs/development/testing.md)
- [Configuration Guide](/docs/getting-started/configuration.md)
