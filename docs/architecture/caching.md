# Caching

This guide covers caching patterns in Wallow using Valkey (a Redis-compatible key-value store).

## Overview

Wallow uses **Valkey** as its distributed caching and real-time infrastructure layer.

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
| `abortConnect` | Don't fail on startup if unavailable | `false` |
| `connectTimeout` | Connection timeout in ms | `5000` |
| `syncTimeout` | Sync operation timeout in ms | `5000` |
| `password` | Authentication password | `your-password` |
| `ssl` | Enable TLS | `true` |
| `allowAdmin` | Enable admin commands (tests only) | `true` |

### Docker Container (Local Development)

The `docker-compose.yml` includes a Valkey container with append-only persistence, authentication, and LRU eviction:

```yaml
valkey:
  image: valkey/valkey:8-alpine
  container_name: ${COMPOSE_PROJECT_NAME:-wallow}-valkey
  command: valkey-server --appendonly yes --requirepass ${VALKEY_PASSWORD} --maxmemory ${VALKEY_MAXMEMORY:-256mb} --maxmemory-policy allkeys-lru
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

### Registration

In `Program.cs`, the `IConnectionMultiplexer` is registered as a singleton with deferred connection. The distributed cache (`IDistributedCache`) is then layered on top, wrapped with `InstrumentedDistributedCache` (`src/Shared/Wallow.Shared.Infrastructure.Core/Cache/InstrumentedDistributedCache.cs`) for cache hit/miss metrics.

## Distributed Caching

### IDistributedCache

For standard caching scenarios, inject `IDistributedCache`. Wallow registers it via `AddStackExchangeRedisCache`, reusing the singleton `IConnectionMultiplexer`.

### Cache Key Patterns

| Pattern | Example | Use Case |
|---------|---------|----------|
| `{domain}:{identifier}` | `feature-flag:dark-mode` | Simple lookups |
| `{domain}:{tenant}:{identifier}` | `meter:abc-123:api.calls:2026-02` | Tenant-scoped data |
| `{domain}:{scope}:{key}` | `presence:{tenantId}:user:user-id-123` | Hierarchical data |

### TTL Strategies

| Strategy | Use Case | Example |
|----------|----------|---------|
| **Absolute Expiration** | Data that becomes stale | Feature flags (5 min) |
| **Sliding Expiration** | Session-like data | User preferences (30 min) |
| **No Expiration + Manual Invalidation** | Rarely changing data | Configuration |
| **Time-Based Keys** | Period-scoped counters | `meter:{tenant}:api.calls:2026-02` |

Use `DistributedCacheEntryOptions` to set `AbsoluteExpirationRelativeToNow`, `SlidingExpiration`, or both (sliding with an absolute cap).

## Caching Patterns

### Cache-Aside

The most common pattern: check cache first, fall back to the data source on a miss, then populate cache.

### Direct Valkey Access

For counters and complex data structures, inject `IConnectionMultiplexer` directly. The `ValkeyMeteringService` (`src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs`) uses this approach for high-performance increment operations and quota checks.

### Batched Operations

Use `IDatabase.CreateBatch()` for related operations that should be sent to Valkey in a single round-trip. The presence tracking service uses batching extensively.

### Cache Invalidation

Three strategies are used:

- **Event-driven invalidation**: Wolverine handlers invalidate cache entries when domain events fire.
- **Time-based expiration**: Entries expire naturally via TTL.
- **Write-through invalidation**: Cache is cleared immediately after a database write.

### Tenant-Scoped Caching

Always include tenant ID in cache keys for multi-tenant data to prevent cross-tenant data leaks.

## SignalR Backplane

Valkey serves as the SignalR backplane for horizontal scaling. The channel prefix is configurable via `SignalR:RedisPrefix` (defaults to `Wallow`). The backplane reuses the singleton `IConnectionMultiplexer`.

When multiple API instances run behind a load balancer, the backplane ensures WebSocket messages reach all connected clients regardless of which instance they are connected to, using Redis pub/sub channels.

## Presence Tracking

The `RedisPresenceService` (`src/Wallow.Api/Services/RedisPresenceService.cs`) tracks online users and their current page context. All presence keys are tenant-scoped.

### Key Structure

| Key Pattern | Type | Purpose |
|-------------|------|---------|
| `presence:{tenantId}:conn2user` | Hash | Maps connectionId to userId |
| `presence:{tenantId}:user:{userId}` | Set | All connectionIds for a user |
| `presence:connpage:{connId}` | String | Current page for a connection |
| `presence:{tenantId}:page:{context}` | Set | All connectionIds viewing a page |
| `presence:conn:tenant:{connId}` | String | Maps connectionId to tenantId (for cleanup) |

All presence keys use a 30-minute TTL as a safety net against orphaned entries.

## API Key Storage

The `RedisApiKeyService` (`src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Services/RedisApiKeyService.cs`) stores service account API keys in Valkey for fast validation. API keys are hashed before storage; the raw key is never persisted.

### Key Structure

| Key Pattern | Content | Expiry |
|-------------|---------|--------|
| `apikey:{hash}` | Full API key metadata | Optional (key expiration) |
| `apikey:id:{keyId}` | Same metadata by ID | Same as above |
| `apikeys:user:{userId}` | Set of keyIds | None |

## Best Practices

1. **Use colons as separators**: `domain:scope:identifier`
2. **Include tenant ID** for multi-tenant data: `tenant:{tenantId}:resource:{id}`
3. **Use time-based suffixes** for period-scoped data: `meter:{tenant}:api.calls:2026-02`
4. **Stagger expiration** with jitter to prevent synchronized cache stampedes
5. **Handle cache failures gracefully**: cache should enhance performance, not be a hard dependency. Fall back to the database when Valkey is unavailable.

## Health Checks

Valkey health is monitored via ASP.NET Core health checks, registered with the `redis` name and `infrastructure` + `ready` tags. Check status at `/health/ready`.

## Testing

### Unit Tests

Mock `IDistributedCache` or `IConnectionMultiplexer` using NSubstitute. For direct Valkey operations, mock `IDatabase` via the multiplexer.

### Integration Tests

Use Testcontainers with the `valkey/valkey:8-alpine` image for integration tests that need a real Valkey instance. The test factory configures `allowAdmin=true` to enable `FlushDatabaseAsync` for state cleanup between tests.

## Related Documentation

- [Testing Guide](../development/testing.md)
- [Configuration Guide](../getting-started/configuration.md)
