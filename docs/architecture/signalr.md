# SignalR Guide

This guide covers real-time communication in Wallow using SignalR with Valkey/Redis backplane for scale-out scenarios.

## Overview

Wallow uses SignalR to provide real-time, bidirectional communication between the server and connected clients. Common use cases include:

- **Notifications**: Push notifications to users instantly when events occur
- **Presence**: Track which users are online and what pages they are viewing
- **Live updates**: Broadcast changes to all interested clients (e.g., collaborative editing, activity feeds)
- **Real-time alerts**: System alerts, announcements, and status updates

### When to Use SignalR

| Use Case | Use SignalR | Alternative |
|----------|-------------|-------------|
| User notifications | Yes | - |
| Presence/online status | Yes | - |
| Live data updates | Yes | Polling for low-frequency updates |
| Chat/messaging | Yes | - |
| Background job progress | Yes | Polling |
| Batch operations | No | RabbitMQ events |
| Module-to-module communication | No | RabbitMQ integration events |

### Architecture

```
Client (Browser/Mobile)
         |
         | WebSocket / LongPolling
         v
    RealtimeHub (/hubs/realtime)
         |
         v
    SignalRRealtimeDispatcher
         |
    +----+----+
    |         |
    v         v
 Users    Groups (Valkey/Redis backplane)
```

## Hub Implementation

Wallow uses a single centralized hub (`RealtimeHub`) that handles all real-time communication. Modules send messages through the `IRealtimeDispatcher` abstraction rather than implementing their own hubs.

### The RealtimeHub

Located at: `/Users/traveler/Repos/Wallow/src/Wallow.Api/Hubs/RealtimeHub.cs`

```csharp
[Authorize]
public sealed class RealtimeHub(
    IPresenceService presenceService,
    IRealtimeDispatcher dispatcher,
    ILogger<RealtimeHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null) { Context.Abort(); return; }

        await presenceService.TrackConnectionAsync(userId, Context.ConnectionId);
        logger.LogInformation("User {UserId} connected with {ConnectionId}", userId, Context.ConnectionId);

        var envelope = RealtimeEnvelope.Create("Presence", "UserOnline", new { UserId = userId });
        await dispatcher.SendToAllAsync(envelope);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = await presenceService.GetUserIdByConnectionAsync(Context.ConnectionId);
        await presenceService.RemoveConnectionAsync(Context.ConnectionId);

        if (userId is not null)
        {
            var stillOnline = await presenceService.IsUserOnlineAsync(userId);
            if (!stillOnline)
            {
                var envelope = RealtimeEnvelope.Create("Presence", "UserOffline", new { UserId = userId });
                await dispatcher.SendToAllAsync(envelope);
            }
        }

        logger.LogInformation("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        logger.LogDebug("Connection {ConnectionId} joined group {GroupId}", Context.ConnectionId, groupId);
    }

    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
    }

    public async Task UpdatePageContext(string pageContext)
    {
        var userId = GetUserId();
        if (userId is null) return;

        await presenceService.SetPageContextAsync(Context.ConnectionId, pageContext);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"page:{pageContext}");

        var viewers = await presenceService.GetUsersOnPageAsync(pageContext);
        var envelope = RealtimeEnvelope.Create("Presence", "PageViewersUpdated", new
        {
            PageContext = pageContext,
            Viewers = viewers
        });
        await Clients.Group($"page:{pageContext}").SendAsync("ReceivePresence", envelope);
    }

    private string? GetUserId() =>
        Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? Context.User?.FindFirst("sub")?.Value;
}
```

### Key Patterns

1. **Authorization Required**: The `[Authorize]` attribute ensures only authenticated users can connect
2. **Presence Tracking**: Connection and disconnection events update the presence service
3. **Group Management**: Clients can join/leave groups for targeted messaging
4. **Page Context**: Track which page each user is viewing for collaborative features

### Method Naming Conventions

| Hub Method | Purpose | Direction |
|------------|---------|-----------|
| `JoinGroup` | Client joins a SignalR group | Client -> Server |
| `LeaveGroup` | Client leaves a SignalR group | Client -> Server |
| `UpdatePageContext` | Client reports current page | Client -> Server |
| `Receive{Module}` | Server sends to client | Server -> Client |

Client receive methods follow the pattern `Receive{Module}` where `{Module}` is the source module name:
- `ReceiveNotifications` - Notification events
- `ReceivePresence` - Presence/online status events
- `ReceiveBilling` - Billing updates (example)

## RealtimeEnvelope

All real-time messages are wrapped in a `RealtimeEnvelope`:

```csharp
public sealed record RealtimeEnvelope(
    string Type,        // Event type (e.g., "NotificationCreated", "UserOnline")
    string Module,      // Source module (e.g., "Notifications", "Presence")
    object Payload,     // Event-specific data
    DateTime Timestamp, // When the event occurred
    string? CorrelationId = null)
{
    public static RealtimeEnvelope Create(string module, string type, object payload, string? correlationId = null)
        => new(type, module, payload, DateTime.UtcNow, correlationId);
}
```

This provides a consistent message structure across all real-time events.

## Valkey/Redis Backplane

### Why a Backplane is Needed

When running multiple API server instances (scale-out), each instance maintains its own set of SignalR connections. Without a backplane:
- A user connected to Server A cannot receive messages sent from Server B
- Groups are local to each server instance

The Redis/Valkey backplane synchronizes messages across all server instances.

### Configuration in Program.cs

```csharp
// Redis connection (deferred for WebApplicationFactory compatibility)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    IConfiguration config = sp.GetRequiredService<IConfiguration>();
    string connectionString = config.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string not configured");
    return ConnectionMultiplexer.Connect(connectionString);
});

// SignalR with Redis backplane — reuses the singleton IConnectionMultiplexer
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Wallow");
    });
builder.Services.AddSingleton<IConfigureOptions<RedisOptions>>(sp =>
{
    IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
    return new ConfigureNamedOptions<RedisOptions>(
        Options.DefaultName,
        options => options.ConnectionFactory = async _ =>
        {
            await Task.CompletedTask;
            return mux;
        });
});
```

### Connection String Setup

In `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

Production configuration options:

```
host:port,password=secret,ssl=true,abortConnect=false,connectTimeout=5000
```

| Option | Description |
|--------|-------------|
| `abortConnect=false` | Don't throw if Redis is unavailable at startup |
| `ssl=true` | Enable TLS for production |
| `password=xxx` | Redis AUTH password |
| `connectTimeout=5000` | Connection timeout in ms |

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

The `RedisPresenceService` implementation uses Redis data structures:
- Hash: `presence:conn2user` - Maps connection ID to user ID
- Set: `presence:user:{userId}` - All connections for a user
- String: `presence:connpage:{connectionId}` - Current page for a connection
- Set: `presence:page:{pageContext}` - All connections viewing a page

## Authentication

### JWT Authentication with SignalR

SignalR uses JWT tokens for authentication. For WebSocket connections, the token is passed via query string since custom headers are not supported.

1. **Client sends token as query parameter** during connection negotiation
2. **TestAuthHandler** (in tests) or **OpenIddict JWT validation** (in production) validates the token
3. **Hub methods** can access `Context.User` for user identity

### Accessing User Identity in Hubs

```csharp
private string? GetUserId() =>
    Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? Context.User?.FindFirst("sub")?.Value;  // OIDC standard "sub" claim
```

The user ID is extracted from:
1. `ClaimTypes.NameIdentifier` - Standard .NET claim
2. `sub` claim - OIDC standard

### Tenant Context in Hubs

For multi-tenant scenarios, you can access tenant information:

```csharp
// Get organization/tenant from claims
var orgId = Context.User?.FindFirst("organization")?.Value;

// Or inject ITenantContext (if configured for SignalR)
public sealed class RealtimeHub(ITenantContext tenantContext, ...) : Hub
{
    public async Task JoinTenantGroup()
    {
        var tenantId = tenantContext.TenantId?.Value.ToString();
        if (tenantId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        }
    }
}
```

Modules use tenant-scoped groups for broadcasting:

```csharp
// In SignalRNotificationService
await _dispatcher.SendToGroupAsync($"tenant:{tenantId.Value}", envelope, cancellationToken);
```

## Client Integration

### JavaScript/TypeScript Client Setup

Install the SignalR client package:

```bash
npm install @microsoft/signalr
```

### Basic Connection

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/realtime", {
        accessTokenFactory: () => getAuthToken()  // Return JWT token
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();
```

### Connection Lifecycle

```typescript
// Start connection
async function startConnection() {
    try {
        await connection.start();
        console.log("SignalR Connected");

        // Join groups after connection
        await connection.invoke("JoinGroup", `tenant:${tenantId}`);
        await connection.invoke("UpdatePageContext", window.location.pathname);
    } catch (err) {
        console.error("SignalR Connection Error:", err);
        // Retry after delay
        setTimeout(startConnection, 5000);
    }
}

// Handle connection state changes
connection.onclose(async (error) => {
    console.log("SignalR Disconnected", error);
});

connection.onreconnecting((error) => {
    console.log("SignalR Reconnecting...", error);
});

connection.onreconnected((connectionId) => {
    console.log("SignalR Reconnected", connectionId);
    // Re-join groups after reconnection
    rejoinGroups();
});
```

### Reconnection Handling

The `withAutomaticReconnect()` method handles reconnection with default retry intervals:
- 0, 2, 10, 30 seconds

Custom retry policy:

```typescript
.withAutomaticReconnect({
    nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.elapsedMilliseconds < 60000) {
            // Retry every 2 seconds for the first minute
            return 2000;
        } else {
            // Then every 30 seconds
            return 30000;
        }
    }
})
```

### Event Subscription Patterns

```typescript
// Module-specific handlers
connection.on("ReceiveNotifications", (envelope: RealtimeEnvelope) => {
    switch (envelope.type) {
        case "NotificationCreated":
            showNotification(envelope.payload);
            break;
        case "AnnouncementPublished":
            showAnnouncement(envelope.payload);
            break;
    }
});

connection.on("ReceivePresence", (envelope: RealtimeEnvelope) => {
    switch (envelope.type) {
        case "UserOnline":
            addOnlineUser(envelope.payload.userId);
            break;
        case "UserOffline":
            removeOnlineUser(envelope.payload.userId);
            break;
        case "PageViewersUpdated":
            updatePageViewers(envelope.payload);
            break;
    }
});

// Type definition
interface RealtimeEnvelope {
    type: string;
    module: string;
    payload: any;
    timestamp: string;
    correlationId?: string;
}
```

### React Hook Example

```typescript
import { useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

export function useSignalR(onNotification: (envelope: RealtimeEnvelope) => void) {
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/realtime", {
                accessTokenFactory: () => getAuthToken()
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveNotifications", onNotification);

        connection.start().catch(console.error);
        connectionRef.current = connection;

        return () => {
            connection.stop();
        };
    }, [onNotification]);

    const joinGroup = useCallback(async (groupId: string) => {
        await connectionRef.current?.invoke("JoinGroup", groupId);
    }, []);

    const leaveGroup = useCallback(async (groupId: string) => {
        await connectionRef.current?.invoke("LeaveGroup", groupId);
    }, []);

    return { joinGroup, leaveGroup };
}
```

## Server-to-Client Messaging

### IRealtimeDispatcher Interface

Modules send real-time messages through the `IRealtimeDispatcher` abstraction:

```csharp
public interface IRealtimeDispatcher
{
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToAllAsync(RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

### SignalRRealtimeDispatcher Implementation

```csharp
public sealed class SignalRRealtimeDispatcher(
    IHubContext<RealtimeHub> hubContext,
    ILogger<SignalRRealtimeDispatcher> logger) : IRealtimeDispatcher
{
    public async Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            var method = $"Receive{envelope.Module}";
            await hubContext.Clients.User(userId).SendAsync(method, envelope, ct);
            logger.LogDebug("Sent {Type} to user {UserId} on {Method}", envelope.Type, userId, method);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send {Type} to user {UserId}", envelope.Type, userId);
        }
    }

    public async Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            var method = $"Receive{envelope.Module}";
            await hubContext.Clients.Group(groupId).SendAsync(method, envelope, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send {Type} to group {GroupId}", envelope.Type, groupId);
        }
    }

    public async Task SendToAllAsync(RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            var method = $"Receive{envelope.Module}";
            await hubContext.Clients.All.SendAsync(method, envelope, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send {Type} to all", envelope.Type);
        }
    }
}
```

### Injecting IRealtimeDispatcher in Services

Modules inject `IRealtimeDispatcher` to send real-time messages:

```csharp
public sealed class SignalRNotificationService : INotificationService
{
    private readonly IRealtimeDispatcher _dispatcher;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IRealtimeDispatcher dispatcher,
        ILogger<SignalRNotificationService> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        var envelope = RealtimeEnvelope.Create("Notifications", "NotificationCreated", payload);
        await _dispatcher.SendToUserAsync(userId.ToString(), envelope, cancellationToken);

        _logger.LogInformation(
            "Sent real-time notification to user {UserId}: {Title}",
            userId, title);
    }

    public async Task BroadcastToTenantAsync(
        TenantId tenantId,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        var envelope = RealtimeEnvelope.Create("Notifications", "AnnouncementPublished", payload);

        // Use tenant ID as the group ID - clients should join their tenant's group
        await _dispatcher.SendToGroupAsync($"tenant:{tenantId.Value}", envelope, cancellationToken);
    }
}
```

### Common Messaging Patterns

**Send to specific user:**
```csharp
var envelope = RealtimeEnvelope.Create("Notifications", "InvoiceCreated", new { InvoiceId = invoiceId });
await _dispatcher.SendToUserAsync(userId.ToString(), envelope);
```

**Send to a group (e.g., tenant members):**
```csharp
var envelope = RealtimeEnvelope.Create("Billing", "InvoiceUpdated", new { Invoice = invoiceDto });
await _dispatcher.SendToGroupAsync($"tenant:{tenantId}", envelope);
```

**Broadcast to tenant:**
```csharp
var envelope = RealtimeEnvelope.Create("Notifications", "SystemAnnouncement", new { Message = message });
await _dispatcher.SendToGroupAsync($"tenant:{tenantId}", envelope);
```

**Broadcast to all connected users:**
```csharp
var envelope = RealtimeEnvelope.Create("Presence", "SystemStatus", new { Status = "maintenance" });
await _dispatcher.SendToAllAsync(envelope);
```

## Testing SignalR

### Unit Testing the Dispatcher

Mock `IHubContext<RealtimeHub>` to test the dispatcher:

```csharp
public class SignalRRealtimeDispatcherTests
{
    private readonly IHubContext<RealtimeHub> _hubContext = Substitute.For<IHubContext<RealtimeHub>>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();
    private readonly SignalRRealtimeDispatcher _sut;

    public SignalRRealtimeDispatcherTests()
    {
        _sut = new SignalRRealtimeDispatcher(_hubContext, NullLogger<SignalRRealtimeDispatcher>.Instance);
    }

    [Fact]
    public async Task SendToUser_ShouldCallCorrectClientMethod()
    {
        _hubContext.Clients.User("user-1").Returns(_clientProxy);
        var envelope = RealtimeEnvelope.Create("Notifications", "InvoiceCreated", new { InvoiceId = 42 });

        await _sut.SendToUserAsync("user-1", envelope);

        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveNotifications",
            Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], envelope)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUser_WhenHubContextThrows_ShouldLogAndNotRethrow()
    {
        _hubContext.Clients.User("user-1").Returns(_clientProxy);
        _clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        var envelope = RealtimeEnvelope.Create("Notifications", "Alert", new { Message = "test" });

        var act = () => _sut.SendToUserAsync("user-1", envelope);

        await act.Should().NotThrowAsync();  // Dispatcher swallows exceptions
    }
}
```

### Integration Testing with Real Connections

Use `WallowApiFactory` and the SignalR client:

```csharp
public class RealtimeHubIntegrationTests : IAsyncLifetime
{
    private WallowApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WallowApiFactory();
        await _factory.InitializeAsync();
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticatedClient_CanConnect()
    {
        await using var connection = CreateHubConnection("user-1");

        await connection.StartAsync();

        connection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task Client_ReceivesNotification()
    {
        const string userId = "user-notif";
        await using var connection = CreateHubConnection(userId);
        var tcs = new TaskCompletionSource<RealtimeEnvelope>();

        connection.On<RealtimeEnvelope>("ReceiveNotifications", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync();
        await Task.Delay(500); // Allow LongPolling cycle to establish

        var dispatcher = _factory.Services.GetRequiredService<IRealtimeDispatcher>();
        await dispatcher.SendToUserAsync(userId, RealtimeEnvelope.Create("Notifications", "InvoiceCreated", new { InvoiceId = 42 }));

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        received.Module.Should().Be("Notifications");
        received.Type.Should().Be("InvoiceCreated");
    }

    [Fact]
    public async Task Client_JoinsGroup_ReceivesGroupMessages()
    {
        await using var connection = CreateHubConnection("user-group");
        var tcs = new TaskCompletionSource<RealtimeEnvelope>();

        connection.On<RealtimeEnvelope>("ReceiveBilling", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", "tenant:test-id");
        await Task.Delay(500);

        var dispatcher = _factory.Services.GetRequiredService<IRealtimeDispatcher>();
        await dispatcher.SendToGroupAsync("tenant:test-id", RealtimeEnvelope.Create("Billing", "InvoiceUpdated", new { InvoiceId = 7 }));

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        received.Type.Should().Be("InvoiceUpdated");
    }

    private HubConnection CreateHubConnection(string userId)
    {
        var token = JwtTokenHelper.GenerateToken(userId);

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/realtime", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();
    }
}
```

### Test Authentication

For SignalR tests, use `JwtTokenHelper` to generate test tokens:

```csharp
public static class JwtTokenHelper
{
    public const string TokenPrefix = "test-token:";

    public static string GenerateToken(
        string userId,
        string? email = null,
        string[]? roles = null)
    {
        // Encode user ID and roles for TestAuthHandler
        var rolesString = roles != null ? string.Join(",", roles) : "admin";
        return $"{TokenPrefix}{userId}:{rolesString}";
    }
}
```

The `TestAuthHandler` parses these tokens and creates the appropriate claims principal.

## Adding a New Hub

While Wallow uses a single `RealtimeHub`, you may need to add specialized hubs for specific features. Follow this checklist:

### Step-by-Step Checklist

1. **Create the Hub Class**

   ```csharp
   // src/Wallow.Api/Hubs/MyFeatureHub.cs
   [Authorize]
   public sealed class MyFeatureHub : Hub
   {
       public override async Task OnConnectedAsync()
       {
           // Track connection
           await base.OnConnectedAsync();
       }

       public override async Task OnDisconnectedAsync(Exception? exception)
       {
           // Clean up
           await base.OnDisconnectedAsync(exception);
       }

       // Client-callable methods
       public async Task SubscribeToFeature(string featureId)
       {
           await Groups.AddToGroupAsync(Context.ConnectionId, $"feature:{featureId}");
       }
   }
   ```

2. **Register the Hub Endpoint**

   In `Program.cs`, add the hub mapping:

   ```csharp
   app.MapHub<MyFeatureHub>("/hubs/myfeature");
   ```

3. **Create a Dispatcher (if needed)**

   If the hub needs server-initiated messaging:

   ```csharp
   public interface IMyFeatureDispatcher
   {
       Task SendToSubscribersAsync(string featureId, object payload, CancellationToken ct = default);
   }

   public class MyFeatureDispatcher(IHubContext<MyFeatureHub> hubContext) : IMyFeatureDispatcher
   {
       public async Task SendToSubscribersAsync(string featureId, object payload, CancellationToken ct)
       {
           await hubContext.Clients.Group($"feature:{featureId}").SendAsync("ReceiveUpdate", payload, ct);
       }
   }
   ```

4. **Register Services**

   In `Program.cs` or a module extension:

   ```csharp
   builder.Services.AddSingleton<IMyFeatureDispatcher, MyFeatureDispatcher>();
   ```

5. **Update Client Code**

   ```typescript
   const featureConnection = new signalR.HubConnectionBuilder()
       .withUrl("/hubs/myfeature", {
           accessTokenFactory: () => getAuthToken()
       })
       .withAutomaticReconnect()
       .build();

   featureConnection.on("ReceiveUpdate", (payload) => {
       // Handle update
   });
   ```

6. **Add Integration Tests**

   ```csharp
   [Fact]
   public async Task MyFeatureHub_SubscribersReceiveUpdates()
   {
       await using var connection = CreateHubConnection("user-1", "/hubs/myfeature");
       var tcs = new TaskCompletionSource<object>();

       connection.On<object>("ReceiveUpdate", payload => tcs.TrySetResult(payload));
       await connection.StartAsync();
       await connection.InvokeAsync("SubscribeToFeature", "feature-123");

       var dispatcher = _factory.Services.GetRequiredService<IMyFeatureDispatcher>();
       await dispatcher.SendToSubscribersAsync("feature-123", new { Data = "test" });

       var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
       received.Should().NotBeNull();
   }
   ```

### Hub Design Considerations

| Decision | Guidance |
|----------|----------|
| Single vs Multiple Hubs | Prefer single `RealtimeHub` for most cases. Add new hubs only for complex features with many dedicated methods |
| Strongly-Typed Hubs | Consider `Hub<IMyHubClient>` for compile-time safety on client methods |
| State Management | Use Redis/Valkey for any state that needs to survive reconnections |
| Error Handling | Catch and log exceptions; avoid exposing internal errors to clients |
| Authentication | Always use `[Authorize]` unless the hub serves anonymous content |

## Troubleshooting

### Common Issues

**Connection fails silently**
- Check browser console for CORS errors
- Verify JWT token is being sent correctly
- Check server logs for authentication failures

**Messages not received**
- Verify client is subscribed to the correct method name (`Receive{Module}`)
- Check if client joined the correct group
- For scale-out: verify Redis backplane is connected

**Reconnection issues**
- Implement `onreconnected` handler to re-join groups
- Check network stability and WebSocket support

**Performance issues**
- Use groups instead of iterating users
- Batch updates when possible
- Consider message size and frequency

### Debugging

Enable SignalR logging:

```typescript
.configureLogging(signalR.LogLevel.Debug)
```

Server-side logging is already configured via Serilog. Look for log entries from `RealtimeHub` and `SignalRRealtimeDispatcher`.
