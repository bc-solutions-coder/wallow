# Audience-Scoped Realtime with SSE + SignalR Split

**Date:** 2026-03-25
**Status:** Approved

## Problem

SignalR handlers broadcast events to entire tenants via `SendToTenantAsync`, leaking sensitive data (e.g., internal inquiry comments, submitter emails) to users who lack the permissions to see them. Additionally, notifications are a one-way data flow being pushed through a bidirectional pipe, which adds unnecessary complexity.

## Solution

Split realtime into two channels:

- **SignalR** — bidirectional (presence, page context, group management)
- **SSE** — one-way server-to-client notification/event delivery with audience scoping

## SSE Endpoint

```
GET /events?subscribe=inquiries,billing
Authorization: Bearer <jwt>
Accept: text/event-stream
```

- Bearer token auth via `Authorization` header
- Query param `subscribe` filters which modules' events are sent
- Single connection per user, client demuxes by `Module`/`Type` from `RealtimeEnvelope`
- Each SSE event is a JSON-serialized `RealtimeEnvelope`

## SSE Infrastructure

- **Redis pub/sub** channels per tenant: `sse:{tenantId}`
- **User-targeted channel**: `sse:user:{userId}`
- Each API instance subscribes to relevant Redis channels
- In-memory `Channel<T>` per SSE connection for last-mile fan-out
- Connection filters events by:
  1. Module subscription (from query param)
  2. Permission (from JWT claims, checked against `RequiredPermission`)
  3. Role (from JWT claims, checked against `RequiredRole`)
  4. User targeting (direct-to-user events via `TargetUserId`)

## ISseDispatcher Interface

```csharp
public interface ISseDispatcher
{
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

`SendToTenantPermissionAsync` and `SendToTenantRoleAsync` set `RequiredPermission`/`RequiredRole` on the envelope before publishing to Redis. The SSE connection inspects these fields to decide whether to forward the event to the client.

## SignalR Changes

SignalR keeps its current `IRealtimeDispatcher` interface unchanged. No new methods. SignalR stays focused on bidirectional concerns:

- Presence (online/offline, page viewers)
- Group management (join/leave)
- Page context updates

Notification handlers migrate from `IRealtimeDispatcher` to `ISseDispatcher`.

## Envelope Metadata

Add optional fields to `RealtimeEnvelope` for SSE-side filtering:

```csharp
public sealed record RealtimeEnvelope(
    string Type,
    string Module,
    object Payload,
    DateTime Timestamp,
    string? CorrelationId = null,
    string? RequiredPermission = null,
    string? RequiredRole = null,
    string? TargetUserId = null);
```

Handlers set these when creating the envelope. The SSE connection uses them to filter. This centralizes filtering logic in the SSE infrastructure rather than spreading it across handlers.

## Handler Migration

| Handler | Before | After |
|---------|--------|-------|
| `InquiryCommentAdded` (internal) | `IRealtimeDispatcher.SendToTenantAsync` | `ISseDispatcher.SendToTenantPermissionAsync("InquiriesRead")` |
| `InquiryCommentAdded` (public) | `IRealtimeDispatcher.SendToTenantAsync` | `ISseDispatcher.SendToTenantAsync` |
| `InquirySubmitted` | `IRealtimeDispatcher.SendToTenantAsync` | `ISseDispatcher.SendToTenantPermissionAsync("InquiriesRead")` |
| `InquiryStatusChanged` | `IRealtimeDispatcher.SendToTenantAsync` | `ISseDispatcher.SendToTenantAsync` |
| `AnnouncementPublished` | `IRealtimeDispatcher.SendToTenantAsync` | `ISseDispatcher.SendToTenantAsync` |
| Presence events | `IRealtimeDispatcher` | **Stays on SignalR** |

## SSE Connection Filtering Flow

```
Event arrives via Redis pub/sub
  -> API instance receives it
    -> For each local SSE connection:
      1. Module in subscribe list? No -> skip
      2. Event has RequiredPermission? Check JWT claims -> skip if missing
      3. Event has RequiredRole? Check JWT claims -> skip if missing
      4. Event has TargetUserId? Check connection user ID -> skip if mismatch
      5. Write to connection's Channel<T>
        -> SSE response stream sends to client
```

## Documentation

Rename `docs/architecture/signalr.md` to `docs/architecture/realtime.md` and update to cover:

- SSE vs SignalR responsibility split
- Audience selection guide (when to use each dispatch method)
- Module subscription guide (for SSE `subscribe` param)
- Mid-session limitation: permission/role filtering uses JWT claims set at connection time; changes require reconnection. This is not a security boundary — API endpoints enforce permissions on every request.
- Handler checklist for new event handlers

## Not in Scope

- Removing SignalR — it stays for bidirectional needs
- BFF changes — BFF continues as-is; this is defense in depth
- Retry/reconnection logic — `EventSource` handles reconnection natively
- Event persistence/replay — missed events during disconnection are lost (same as current SignalR behavior)
