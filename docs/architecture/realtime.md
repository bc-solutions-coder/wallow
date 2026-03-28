# Realtime

Wallow's realtime system is split into two channels:

- **SSE (Server-Sent Events)** -- one-way server-to-client delivery for notifications and events, with audience scoping by permission, role, or user
- **SignalR** -- bidirectional communication for presence, page context, and group management

## Architecture

```
                        +-------------------------------------------+
                        |              Client (Browser)             |
                        |                                           |
                        |   EventSource (/events)    SignalR Hub    |
                        |   <- notifications          <-> presence  |
                        |   <- events                 <-> groups    |
                        |   <- alerts                 <-> page ctx  |
                        +------+----------------------------+-------+
                               |                            |
                    SSE (text/event-stream)           WebSocket/LP
                               |                            |
                        +------v----------------------------v-------+
                        |                 API Instance               |
                        |                                           |
                        |   SseEndpoint        RealtimeHub          |
                        |       |                   |               |
                        |   SseConnectionManager    |               |
                        |       |              PresenceService      |
                        |   SseRedisSubscriber      |               |
                        +------+--------------------+---------------+
                               |                    |
                        Redis pub/sub          Redis backplane
                        (sse:tenant:*          (SignalR scale-out)
                         sse:user:*)
```

## When to Use Each Channel

| Use Case | Channel | Why |
|----------|---------|-----|
| Push notifications | SSE | One-way, supports audience scoping |
| Event broadcasts (inquiry updates, billing) | SSE | One-way with permission filtering |
| Presence / online status | SignalR | Bidirectional, needs group management |
| Page context tracking | SignalR | Bidirectional, updates shared state |
| Group join/leave | SignalR | Requires client-initiated actions |

## SSE (Server-Sent Events)

### Endpoint

```
GET /events?subscribe=inquiries,billing,notifications
Authorization: Bearer <jwt>
Accept: text/event-stream
```

Authentication is via Bearer token. The `subscribe` query param limits which modules' events are delivered. Each SSE event is a JSON-serialized `RealtimeEnvelope`.

### ISseDispatcher

Modules send events through `ISseDispatcher` (in `src/Shared/Wallow.Shared.Contracts/Realtime/ISseDispatcher.cs`):

```csharp
public interface ISseDispatcher
{
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

### Audience Selection

| Method | When to Use | Example |
|--------|-------------|---------|
| `SendToTenantAsync` | All tenant members should see it | Announcement published, inquiry status changed |
| `SendToTenantPermissionAsync` | Only users with a specific permission | Internal inquiry comment (`inquiries.manage`) |
| `SendToTenantRoleAsync` | Only users in a specific role | Admin-only alerts |
| `SendToUserAsync` | Targeted to one user | Personal notification, direct message |

### RealtimeEnvelope

`RealtimeEnvelope` (in `src/Shared/Wallow.Shared.Contracts/Realtime/RealtimeEnvelope.cs`) carries the event payload and optional audience-scoping fields:

```csharp
public sealed record RealtimeEnvelope(
    string Type,
    string Module,
    object Payload,
    DateTime Timestamp,
    string? CorrelationId = null,
    string? RequiredPermission = null,
    string? RequiredRole = null,
    string? TargetUserId = null)
{
    public static RealtimeEnvelope Create(string module, string type, object payload, string? correlationId = null)
        => new(type, module, payload, DateTime.UtcNow, correlationId);
}
```

The `ISseDispatcher` implementation stamps `RequiredPermission`, `RequiredRole`, or `TargetUserId` onto the envelope before publishing to Redis. The SSE connection inspects these fields to decide whether to forward the event.

### SSE Connection Filtering

When an event arrives via Redis pub/sub, each local SSE connection applies these filters in order:

1. Module in subscribe list? No -- skip
2. `RequiredPermission` set? Check JWT claims -- skip if missing
3. `RequiredRole` set? Check JWT claims -- skip if missing
4. `TargetUserId` set? Check connection user ID -- skip if mismatch
5. Write to connection's bounded `Channel<T>` and send via SSE response stream

### SSE Infrastructure

| Component | Location | Purpose |
|-----------|----------|---------|
| `ISseDispatcher` | `src/Shared/Wallow.Shared.Contracts/Realtime/` | Dispatch interface for modules |
| `RedisSseDispatcher` | `src/Wallow.Api/Services/` | Publishes events to Redis channels |
| `SseConnectionManager` | `src/Wallow.Api/Services/` | Tracks active connections and filters delivery |
| `SseConnectionState` | `src/Wallow.Api/Services/` | Per-connection metadata (user, tenant, modules, permissions, roles) |
| `SseRedisSubscriber` | `src/Wallow.Api/Services/` | Background service that subscribes to Redis and fans out to connections |
| `SseEndpoint` | `src/Wallow.Api/Endpoints/` | HTTP GET `/events` endpoint |

### Redis Channel Naming

| Channel Pattern | Purpose |
|----------------|---------|
| `sse:tenant:{tenantId}` | Tenant-scoped events (broadcast, permission-scoped, role-scoped) |
| `sse:user:{userId}` | User-targeted events |

### Mid-Session JWT Limitation

SSE connections extract permissions and roles from JWT claims at connection time. If a user's permissions or roles change mid-session, the SSE connection continues filtering based on the original claims until the client reconnects.

This is not a security boundary -- API endpoints enforce permissions on every request. The SSE filter prevents leaking event data to the client UI, but the source of truth for authorization remains the API layer.

## SignalR

SignalR handles bidirectional real-time features. It is not used for notifications or event broadcasts -- those go through SSE.

### RealtimeHub

Located at `src/Wallow.Api/Hubs/RealtimeHub.cs`, mapped to `/hubs/realtime`.

| Hub Method | Purpose | Direction |
|------------|---------|-----------|
| `JoinGroup` | Client joins a SignalR group (validated against allowed prefixes and tenant) | Client -> Server |
| `LeaveGroup` | Client leaves a SignalR group | Client -> Server |
| `UpdatePageContext` | Client reports current page; triggers `PageViewersUpdated` broadcast | Client -> Server |

On connect, the hub automatically joins the user to their tenant group (`tenant:{tenantId}`) and, for staff users (admin/manager roles), also to `tenant:{tenantId}:staff`. A `UserOnline` presence event is broadcast to the tenant group. On disconnect, a `UserOffline` event is broadcast if the user has no remaining connections.

### IRealtimeDispatcher

SignalR's dispatch interface (in `src/Shared/Wallow.Shared.Contracts/Realtime/IRealtimeDispatcher.cs`):

```csharp
public interface IRealtimeDispatcher
{
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

### Presence Service

`IPresenceService` (in `src/Shared/Wallow.Shared.Contracts/Realtime/IPresenceService.cs`) tracks user presence across server instances using Redis. All operations are tenant-scoped:

```csharp
public interface IPresenceService
{
    Task TrackConnectionAsync(Guid tenantId, string userId, string connectionId, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
    Task SetPageContextAsync(Guid tenantId, string connectionId, string pageContext, CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(Guid tenantId, string pageContext, CancellationToken ct = default);
    Task<bool> IsUserOnlineAsync(Guid tenantId, string userId, CancellationToken ct = default);
    Task<string?> GetUserIdByConnectionAsync(string connectionId, CancellationToken ct = default);
}
```

Redis data structures for presence (tenant-scoped):

| Key Pattern | Type | Purpose |
|-------------|------|---------|
| `presence:{tenantId}:conn2user` | Hash | Maps connection ID to user ID |
| `presence:{tenantId}:user:{userId}` | Set | All connections for a user |
| `presence:connpage:{connectionId}` | String | Current page for a connection |
| `presence:{tenantId}:page:{pageContext}` | Set | All connections viewing a page |
| `presence:conn:tenant:{connectionId}` | String | Maps connection to its tenant (for cleanup) |

### Group Naming Conventions

| Group Pattern | Purpose |
|---------------|---------|
| `tenant:{tenantId}` | All members of a tenant |
| `tenant:{tenantId}:staff` | Admin and manager users in a tenant |
| `page:{tenantId}:{pageContext}` | Users viewing a specific page |

### SignalR Backplane

SignalR uses Redis/Valkey as a backplane to synchronize messages across API instances. The backplane reuses the singleton `IConnectionMultiplexer` registered in `Program.cs`.

## Handler Checklist

When adding a new event handler that sends realtime events:

1. **Sensitive data?** Use `SendToTenantPermissionAsync` or `SendToTenantRoleAsync`; otherwise use `SendToTenantAsync` for broadcast
2. **Targeted to a specific user?** Use `SendToUserAsync`
3. **Bidirectional (needs client response)?** Use SignalR (`IRealtimeDispatcher`), not SSE
4. **Set the `module` parameter** in `RealtimeEnvelope.Create()` so clients can filter by subscription

## Current SSE Handler Reference

| Handler | Dispatch Method | Audience |
|---------|----------------|----------|
| `InquiryCommentAddedSseHandler` (internal) | `SendToTenantPermissionAsync` | `inquiries.manage` |
| `InquiryCommentAddedSseHandler` (public) | `SendToTenantAsync` | All tenant members |
| `InquirySubmittedSseHandler` | `SendToTenantPermissionAsync` | `inquiries.read` |
| `InquiryStatusChangedSseHandler` | `SendToTenantPermissionAsync` | `inquiries.read` |
| `SseNotificationService.BroadcastToTenantAsync` | `SendToTenantAsync` | All tenant members |
| `SseNotificationService.SendToUserAsync` | `SendToUserAsync` | Specific user |
| Presence events | **SignalR** (`IRealtimeDispatcher`) | Via SignalR groups |

## Troubleshooting

### SSE Issues

**Events not arriving:**
- Verify the `subscribe` query param includes the event's module
- Check that the user's JWT contains the required permission/role claims
- Verify Redis pub/sub is connected (check `SseRedisSubscriber` logs)

**Stale permission filtering:**
- Permissions are read from JWT at connection time -- if permissions changed, the client must reconnect

**Connection drops:**
- `EventSource` handles automatic reconnection
- Check server logs for connection cleanup in `SseEndpoint`

### SignalR Issues

**Connection fails silently:**
- Check browser console for CORS errors
- Verify JWT token is being sent correctly
- Check server logs for authentication failures

**Presence not updating:**
- Verify client joined the correct group after connection/reconnection
- Check Redis connectivity for backplane

### Debugging

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Wallow.Api.Services.SseConnectionManager": "Debug",
      "Wallow.Api.Services.SseRedisSubscriber": "Debug",
      "Wallow.Api.Hubs.RealtimeHub": "Debug"
    }
  }
}
```
