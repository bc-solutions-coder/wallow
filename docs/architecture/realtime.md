# Realtime Guide

This guide covers real-time communication in Wallow. The realtime system is split into two channels:

- **SSE (Server-Sent Events)** — one-way server-to-client delivery for notifications and events, with audience scoping by permission, role, or user
- **SignalR** — bidirectional communication for presence, page context, and group management

## Architecture Overview

```
                        ┌─────────────────────────────────────────┐
                        │              Client (Browser)           │
                        │                                         │
                        │   EventSource (/events)    SignalR Hub  │
                        │   ← notifications          ↔ presence  │
                        │   ← events                 ↔ groups    │
                        │   ← alerts                 ↔ page ctx  │
                        └──────┬──────────────────────────┬───────┘
                               │                          │
                    SSE (text/event-stream)         WebSocket/LP
                               │                          │
                        ┌──────▼──────────────────────────▼───────┐
                        │                 API Instance             │
                        │                                         │
                        │   SseEndpoint        RealtimeHub        │
                        │       │                   │             │
                        │   SseConnectionManager    │             │
                        │       │              PresenceService    │
                        │   SseRedisSubscriber      │             │
                        └──────┬────────────────────┬─────────────┘
                               │                    │
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

- **Authentication**: Bearer token via `Authorization` header
- **Module filtering**: `subscribe` query param limits which modules' events are delivered
- **Single connection**: One SSE connection per user; the client demuxes events by `Module` and `Type` from `RealtimeEnvelope`
- **Format**: Each SSE event is a JSON-serialized `RealtimeEnvelope`

### ISseDispatcher

Modules send events through `ISseDispatcher` (in `Wallow.Shared.Contracts`):

```csharp
public interface ISseDispatcher
{
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

### Audience Selection Guide

Choose the dispatch method based on who should receive the event:

| Method | When to Use | Example |
|--------|-------------|---------|
| `SendToTenantAsync` | All tenant members should see it | Announcement published, inquiry status changed |
| `SendToTenantPermissionAsync` | Only users with a specific permission | Internal inquiry comment (`inquiries.read`), new inquiry submitted |
| `SendToTenantRoleAsync` | Only users in a specific role | Admin-only alerts |
| `SendToUserAsync` | Targeted to one user | Personal notification, direct message |

### Envelope Metadata

`RealtimeEnvelope` carries optional audience-scoping fields:

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

### SSE Connection Filtering Flow

When an event arrives via Redis pub/sub, each local SSE connection applies these filters in order:

```
Event arrives via Redis pub/sub
  → API instance receives it
    → For each local SSE connection:
      1. Module in subscribe list? No → skip
      2. RequiredPermission set? Check JWT claims → skip if missing
      3. RequiredRole set? Check JWT claims → skip if missing
      4. TargetUserId set? Check connection user ID → skip if mismatch
      5. Write to connection's bounded Channel<T>
        → SSE response stream sends to client
```

### SSE Infrastructure Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ISseDispatcher` | `Shared.Contracts/Realtime/` | Dispatch interface for modules |
| `RedisSseDispatcher` | `Wallow.Api/Services/` | Publishes events to Redis channels |
| `SseConnectionManager` | `Wallow.Api/Services/` | Tracks active connections and filters delivery |
| `SseConnectionState` | `Wallow.Api/Services/` | Per-connection metadata (user, tenant, modules, permissions, roles) |
| `SseRedisSubscriber` | `Wallow.Api/Services/` | Background service that subscribes to Redis and fans out to connections |
| `SseEndpoint` | `Wallow.Api/Endpoints/` | HTTP GET `/events` endpoint |

### Redis Channel Naming

| Channel Pattern | Purpose |
|----------------|---------|
| `sse:tenant:{tenantId}` | Tenant-scoped events (broadcast, permission-scoped, role-scoped) |
| `sse:user:{userId}` | User-targeted events |

### Client Integration (SSE)

Use the native `EventSource` API:

```typescript
const token = getAuthToken();
const eventSource = new EventSource(
    `/events?subscribe=inquiries,notifications`,
    // Note: EventSource doesn't support Authorization headers natively.
    // Use a polyfill like eventsource-polyfill or pass token via cookie/query param.
);

eventSource.onmessage = (event) => {
    const envelope: RealtimeEnvelope = JSON.parse(event.data);

    switch (`${envelope.module}:${envelope.type}`) {
        case "Notifications:NotificationCreated":
            showNotification(envelope.payload);
            break;
        case "Inquiries:InquirySubmitted":
            refreshInquiryList();
            break;
    }
};

eventSource.onerror = () => {
    // EventSource handles reconnection automatically
    console.warn("SSE connection lost, reconnecting...");
};

interface RealtimeEnvelope {
    type: string;
    module: string;
    payload: any;
    timestamp: string;
    correlationId?: string;
    requiredPermission?: string;
    requiredRole?: string;
    targetUserId?: string;
}
```

### Mid-Session JWT Limitation

> **Important:** SSE connections extract permissions and roles from JWT claims at connection time. If a user's permissions or roles change mid-session, the SSE connection continues filtering based on the original claims until the client reconnects.
>
> This is **not a security boundary** — API endpoints enforce permissions on every request. The SSE filter is defense-in-depth to avoid leaking event data to the client UI, but the source of truth for authorization remains the API layer.
>
> Changes that require reconnection: role assignment changes, permission grants/revocations, tenant membership changes.

## SignalR

SignalR remains for bidirectional real-time features. It is **not used for notifications or event broadcasts** — those go through SSE.

### RealtimeHub

Located at `src/Wallow.Api/Hubs/RealtimeHub.cs`, mapped to `/hubs/realtime`.

| Hub Method | Purpose | Direction |
|------------|---------|-----------|
| `JoinGroup` | Client joins a SignalR group | Client → Server |
| `LeaveGroup` | Client leaves a SignalR group | Client → Server |
| `UpdatePageContext` | Client reports current page | Client → Server |
| `Receive{Module}` | Server sends to client | Server → Client |

### IRealtimeDispatcher

SignalR's dispatch interface for presence and group messaging:

```csharp
public interface IRealtimeDispatcher
{
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToAllAsync(RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

### Presence Service

Wallow uses Redis to track user presence across server instances:

```csharp
public interface IPresenceService
{
    Task TrackConnectionAsync(string userId, string connectionId, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
    Task SetPageContextAsync(string connectionId, string pageContext, CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(string pageContext, CancellationToken ct = default);
    Task<bool> IsUserOnlineAsync(string userId, CancellationToken ct = default);
    Task<string?> GetUserIdByConnectionAsync(string connectionId, CancellationToken ct = default);
}
```

Redis data structures for presence:

| Key Pattern | Type | Purpose |
|-------------|------|---------|
| `presence:conn2user` | Hash | Maps connection ID → user ID |
| `presence:user:{userId}` | Set | All connections for a user |
| `presence:connpage:{connectionId}` | String | Current page for a connection |
| `presence:page:{pageContext}` | Set | All connections viewing a page |

### Group Naming Conventions

| Group Pattern | Purpose |
|---------------|---------|
| `tenant:{tenantId}` | All members of a tenant |
| `page:{pageContext}` | Users viewing a specific page |

### SignalR Backplane

SignalR uses Redis/Valkey as a backplane to synchronize messages across API instances. Configuration is in `Program.cs` — the SignalR backplane reuses the singleton `IConnectionMultiplexer`.

### Client Integration (SignalR)

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/realtime", {
        accessTokenFactory: () => getAuthToken()
    })
    .withAutomaticReconnect()
    .build();

// Presence events
connection.on("ReceivePresence", (envelope) => {
    switch (envelope.type) {
        case "UserOnline": addOnlineUser(envelope.payload.userId); break;
        case "UserOffline": removeOnlineUser(envelope.payload.userId); break;
        case "PageViewersUpdated": updatePageViewers(envelope.payload); break;
    }
});

await connection.start();
await connection.invoke("JoinGroup", `tenant:${tenantId}`);
await connection.invoke("UpdatePageContext", window.location.pathname);
```

## Handler Checklist

When adding a new event handler that sends realtime events, use this checklist:

1. **Does the event contain sensitive data?**
   - Yes → Use `SendToTenantPermissionAsync` or `SendToTenantRoleAsync` with the appropriate permission/role
   - No → Use `SendToTenantAsync` for broadcast

2. **Is the event targeted to a specific user?**
   - Yes → Use `SendToUserAsync`

3. **Is the event bidirectional (needs client response)?**
   - Yes → Use SignalR (`IRealtimeDispatcher`), not SSE
   - No → Use SSE (`ISseDispatcher`)

4. **Which module does the event belong to?**
   - Set the `module` parameter in `RealtimeEnvelope.Create()` so clients can filter by subscription

5. **Example handler:**

```csharp
public static async Task Handle(
    MyDomainEvent @event,
    ISseDispatcher dispatcher,
    ITenantContext tenantContext)
{
    RealtimeEnvelope envelope = RealtimeEnvelope.Create(
        module: "MyModule",
        type: "MyEventOccurred",
        payload: new { @event.RelevantField });

    // Choose the appropriate dispatch method:
    // Public event → SendToTenantAsync
    // Sensitive event → SendToTenantPermissionAsync
    // Role-restricted → SendToTenantRoleAsync
    // User-specific → SendToUserAsync

    await dispatcher.SendToTenantAsync(
        tenantContext.TenantId!.Value.Value,
        envelope);
}
```

## Current Handler Migration Reference

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

**Events not arriving**
- Verify the `subscribe` query param includes the event's module
- Check that the user's JWT contains the required permission/role claims
- Verify Redis pub/sub is connected (check `SseRedisSubscriber` logs)

**Stale permission filtering**
- Permissions are read from JWT at connection time — if permissions changed, the client must reconnect
- This is by design; see [Mid-Session JWT Limitation](#mid-session-jwt-limitation)

**Connection drops**
- `EventSource` handles automatic reconnection
- Check server logs for connection cleanup in `SseEndpoint`

### SignalR Issues

**Connection fails silently**
- Check browser console for CORS errors
- Verify JWT token is being sent correctly
- Check server logs for authentication failures

**Presence not updating**
- Verify client joined the correct group after connection/reconnection
- Check Redis connectivity for backplane

### Debugging

Enable detailed logging for realtime components:

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
