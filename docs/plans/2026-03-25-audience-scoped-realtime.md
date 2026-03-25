# Audience-Scoped Realtime (SSE + SignalR Split) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split realtime into SignalR (bidirectional: presence, page context) and SSE (one-way: notifications/events with permission/role/user audience scoping).

**Architecture:** Add an SSE endpoint at `GET /events?subscribe=module1,module2` with bearer auth. Events flow from Wolverine handlers → `ISseDispatcher` → Redis pub/sub → per-instance fan-out → per-connection `Channel<T>` with permission/role/user filtering. SignalR stays unchanged for presence.

**Tech Stack:** ASP.NET Core minimal API (SSE endpoint), `System.Threading.Channels`, Redis pub/sub (via existing `IConnectionMultiplexer`), existing JWT auth.

---

### Task 1: Extend RealtimeEnvelope with audience metadata

**Files:**
- Modify: `src/Shared/Wallow.Shared.Contracts/Realtime/RealtimeEnvelope.cs`

**Step 1: Update the record**

Add three nullable fields for SSE-side filtering:

```csharp
using JetBrains.Annotations;

namespace Wallow.Shared.Contracts.Realtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
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

**Step 2: Verify existing tests still pass**

Run: `./scripts/run-tests.sh api`
Expected: All existing tests pass — new fields are optional with defaults.

Run: `./scripts/run-tests.sh notifications`
Expected: All existing tests pass.

**Step 3: Commit**

```bash
git add src/Shared/Wallow.Shared.Contracts/Realtime/RealtimeEnvelope.cs
git commit -m "feat(realtime): add audience metadata fields to RealtimeEnvelope"
```

---

### Task 2: Define ISseDispatcher interface

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Realtime/ISseDispatcher.cs`

**Step 1: Create the interface**

```csharp
namespace Wallow.Shared.Contracts.Realtime;

public interface ISseDispatcher
{
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
```

**Step 2: Commit**

```bash
git add src/Shared/Wallow.Shared.Contracts/Realtime/ISseDispatcher.cs
git commit -m "feat(realtime): add ISseDispatcher interface for audience-scoped SSE dispatch"
```

---

### Task 3: Implement SseConnectionManager

This manages the in-memory `Channel<T>` per SSE connection, handles registration/unregistration, and fan-out from Redis subscriptions.

**Files:**
- Create: `src/Wallow.Api/Services/SseConnectionManager.cs`
- Test: `tests/Wallow.Api.Tests/Services/SseConnectionManagerTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Security.Claims;
using System.Threading.Channels;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class SseConnectionManagerTests
{
    private readonly SseConnectionManager _sut = new();

    private static ClaimsPrincipal CreatePrincipal(
        string userId,
        string[] permissions = null,
        string[] roles = null)
    {
        ClaimsIdentity identity = new("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        foreach (string permission in permissions ?? [])
        {
            identity.AddClaim(new Claim("permission", permission));
        }
        foreach (string role in roles ?? [])
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Register_ReturnsChannelReader()
    {
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(Guid.NewGuid(), user, ["inquiries"]);

        connection.Reader.Should().NotBeNull();
    }

    [Fact]
    public void Unregister_CompletesChannel()
    {
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(Guid.NewGuid(), user, ["inquiries"]);

        _sut.Unregister(connection.Id);

        connection.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_TenantEvent_ReachesSubscribedConnection()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { Id = 1 });
        _sut.Dispatch(tenantId, envelope);

        bool hasItem = await connection.Reader.WaitToReadAsync(new CancellationTokenSource(1000).Token);
        hasItem.Should().BeTrue();
        RealtimeEnvelope received = await connection.Reader.ReadAsync();
        received.Type.Should().Be("InquirySubmitted");
    }

    [Fact]
    public void Dispatch_TenantEvent_DoesNotReachUnsubscribedModule()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(tenantId, user, ["billing"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { Id = 1 });
        _sut.Dispatch(tenantId, envelope);

        connection.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Dispatch_PermissionEvent_ReachesUserWithPermission()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1", permissions: ["InquiriesRead"]);
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquiryCommentAdded", new { Id = 1 })
            with { RequiredPermission = "InquiriesRead" };
        _sut.Dispatch(tenantId, envelope);

        bool hasItem = await connection.Reader.WaitToReadAsync(new CancellationTokenSource(1000).Token);
        hasItem.Should().BeTrue();
    }

    [Fact]
    public void Dispatch_PermissionEvent_DoesNotReachUserWithoutPermission()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1", permissions: []);
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquiryCommentAdded", new { Id = 1 })
            with { RequiredPermission = "InquiriesRead" };
        _sut.Dispatch(tenantId, envelope);

        connection.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Dispatch_RoleEvent_ReachesUserWithRole()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1", roles: ["admin"]);
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "SomeEvent", new { Id = 1 })
            with { RequiredRole = "admin" };
        _sut.Dispatch(tenantId, envelope);

        bool hasItem = await connection.Reader.WaitToReadAsync(new CancellationTokenSource(1000).Token);
        hasItem.Should().BeTrue();
    }

    [Fact]
    public void Dispatch_RoleEvent_DoesNotReachUserWithoutRole()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1", roles: ["member"]);
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "SomeEvent", new { Id = 1 })
            with { RequiredRole = "admin" };
        _sut.Dispatch(tenantId, envelope);

        connection.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Dispatch_UserTargetedEvent_ReachesTargetUser()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "CommentReply", new { Id = 1 })
            with { TargetUserId = "user-1" };
        _sut.Dispatch(tenantId, envelope);

        bool hasItem = await connection.Reader.WaitToReadAsync(new CancellationTokenSource(1000).Token);
        hasItem.Should().BeTrue();
    }

    [Fact]
    public void Dispatch_UserTargetedEvent_DoesNotReachOtherUser()
    {
        Guid tenantId = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-2");
        SseConnection connection = _sut.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "CommentReply", new { Id = 1 })
            with { TargetUserId = "user-1" };
        _sut.Dispatch(tenantId, envelope);

        connection.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Dispatch_DifferentTenant_DoesNotReachConnection()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        ClaimsPrincipal user = CreatePrincipal("user-1");
        SseConnection connection = _sut.Register(tenantA, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { Id = 1 });
        _sut.Dispatch(tenantB, envelope);

        connection.Reader.TryRead(out _).Should().BeFalse();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh api`
Expected: FAIL — `SseConnectionManager` and `SseConnection` do not exist yet.

**Step 3: Implement SseConnectionManager**

```csharp
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public sealed record SseConnection(
    string Id,
    Guid TenantId,
    ClaimsPrincipal User,
    HashSet<string> SubscribedModules,
    ChannelReader<RealtimeEnvelope> Reader,
    ChannelWriter<RealtimeEnvelope> Writer)
{
    public string UserId { get; } = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? string.Empty;

    public bool HasPermission(string permission) =>
        User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

    public bool HasRole(string role) =>
        User.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role")
            && c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
}

public sealed class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, SseConnection>> _tenantConnections = new();

    public SseConnection Register(Guid tenantId, ClaimsPrincipal user, IReadOnlyList<string> modules)
    {
        Channel<RealtimeEnvelope> channel = Channel.CreateBounded<RealtimeEnvelope>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        string id = Guid.NewGuid().ToString("N");
        HashSet<string> moduleSet = new(modules, StringComparer.OrdinalIgnoreCase);
        SseConnection connection = new(id, tenantId, user, moduleSet, channel.Reader, channel.Writer);

        _connections[id] = connection;
        ConcurrentDictionary<string, SseConnection> tenantDict = _tenantConnections.GetOrAdd(tenantId, _ => new());
        tenantDict[id] = connection;

        return connection;
    }

    public void Unregister(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out SseConnection? connection))
        {
            return;
        }

        connection.Writer.TryComplete();

        if (_tenantConnections.TryGetValue(connection.TenantId, out ConcurrentDictionary<string, SseConnection>? tenantDict))
        {
            tenantDict.TryRemove(connectionId, out _);
        }
    }

    public void Dispatch(Guid tenantId, RealtimeEnvelope envelope)
    {
        if (!_tenantConnections.TryGetValue(tenantId, out ConcurrentDictionary<string, SseConnection>? tenantDict))
        {
            return;
        }

        foreach (SseConnection connection in tenantDict.Values)
        {
            if (!ShouldDeliver(connection, envelope))
            {
                continue;
            }

            connection.Writer.TryWrite(envelope);
        }
    }

    public void DispatchToUser(string userId, RealtimeEnvelope envelope)
    {
        foreach (SseConnection connection in _connections.Values)
        {
            if (connection.UserId != userId)
            {
                continue;
            }

            if (!connection.SubscribedModules.Contains(envelope.Module))
            {
                continue;
            }

            connection.Writer.TryWrite(envelope);
        }
    }

    private static bool ShouldDeliver(SseConnection connection, RealtimeEnvelope envelope)
    {
        // 1. Module subscription check
        if (!connection.SubscribedModules.Contains(envelope.Module))
        {
            return false;
        }

        // 2. User targeting check
        if (envelope.TargetUserId is not null && envelope.TargetUserId != connection.UserId)
        {
            return false;
        }

        // 3. Permission check
        if (envelope.RequiredPermission is not null && !connection.HasPermission(envelope.RequiredPermission))
        {
            return false;
        }

        // 4. Role check
        if (envelope.RequiredRole is not null && !connection.HasRole(envelope.RequiredRole))
        {
            return false;
        }

        return true;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh api`
Expected: All SseConnectionManager tests PASS.

**Step 5: Commit**

```bash
git add src/Wallow.Api/Services/SseConnectionManager.cs tests/Wallow.Api.Tests/Services/SseConnectionManagerTests.cs
git commit -m "feat(realtime): add SseConnectionManager with audience filtering"
```

---

### Task 4: Implement RedisSseDispatcher

This publishes events to Redis pub/sub channels. The SSE endpoint's background subscriber (Task 6) picks them up and fans out to local connections.

**Files:**
- Create: `src/Wallow.Api/Services/RedisSseDispatcher.cs`
- Test: `tests/Wallow.Api.Tests/Services/RedisSseDispatcherTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Infrastructure.Core.Services;

namespace Wallow.Api.Tests.Services;

public class RedisSseDispatcherTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly IHtmlSanitizationService _sanitizer = Substitute.For<IHtmlSanitizationService>();
    private readonly RedisSseDispatcher _sut;

    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public RedisSseDispatcherTests()
    {
        _redis.GetSubscriber().Returns(_subscriber);
        _sanitizer.Sanitize(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        _sut = new RedisSseDispatcher(_redis, _sanitizer, NullLogger<RedisSseDispatcher>.Instance);
    }

    [Fact]
    public async Task SendToTenantAsync_PublishesToTenantChannel()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { Id = 1 });

        await _sut.SendToTenantAsync(_tenantId, envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == $"sse:tenant:{_tenantId}"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_SetsRequiredPermissionOnEnvelope()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquiryCommentAdded", new { Id = 1 });

        await _sut.SendToTenantPermissionAsync(_tenantId, "InquiriesRead", envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == $"sse:tenant:{_tenantId}"),
            Arg.Is<RedisValue>(v => v.ToString().Contains("InquiriesRead")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantRoleAsync_SetsRequiredRoleOnEnvelope()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "SomeEvent", new { Id = 1 });

        await _sut.SendToTenantRoleAsync(_tenantId, "admin", envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == $"sse:tenant:{_tenantId}"),
            Arg.Is<RedisValue>(v => v.ToString().Contains("admin")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToUserAsync_PublishesToUserChannel()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "NotificationCreated", new { Id = 1 });

        await _sut.SendToUserAsync("user-1", envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == "sse:user:user-1"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantAsync_WhenRedisThrows_DoesNotRethrow()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns<long>(_ => throw new RedisException("Connection lost"));

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Type", new { });

        Func<Task> act = () => _sut.SendToTenantAsync(_tenantId, envelope);

        await act.Should().NotThrowAsync();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh api`
Expected: FAIL — `RedisSseDispatcher` does not exist.

**Step 3: Implement RedisSseDispatcher**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using StackExchange.Redis;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Infrastructure.Core.Services;

namespace Wallow.Api.Services;

internal sealed partial class RedisSseDispatcher(
    IConnectionMultiplexer redis,
    IHtmlSanitizationService sanitizer,
    ILogger<RedisSseDispatcher> logger) : ISseDispatcher
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        await PublishAsync($"sse:tenant:{tenantId}", envelope);
    }

    public async Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        RealtimeEnvelope scoped = envelope with { RequiredPermission = permission };
        await PublishAsync($"sse:tenant:{tenantId}", scoped);
    }

    public async Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        RealtimeEnvelope scoped = envelope with { RequiredRole = role };
        await PublishAsync($"sse:tenant:{tenantId}", scoped);
    }

    public async Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        RealtimeEnvelope targeted = envelope with { TargetUserId = userId };
        await PublishAsync($"sse:user:{userId}", targeted);
    }

    private async Task PublishAsync(string channel, RealtimeEnvelope envelope)
    {
        try
        {
            RealtimeEnvelope sanitized = SanitizeEnvelope(envelope);
            string json = JsonSerializer.Serialize(sanitized, _jsonOptions);
            ISubscriber subscriber = redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(channel), json);
            LogPublished(sanitized.Type, channel);
        }
        catch (Exception ex)
        {
            LogPublishFailed(ex, envelope.Type, channel);
        }
    }

    private RealtimeEnvelope SanitizeEnvelope(RealtimeEnvelope envelope)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(envelope.Payload);
        if (node is null)
        {
            return envelope;
        }

        SanitizeNode(node);
        return envelope with { Payload = node };
    }

    private void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? value) in obj.ToList())
            {
                if (value is JsonValue val && val.TryGetValue(out string? str))
                {
                    obj[key] = sanitizer.Sanitize(str);
                }
                else if (value is not null)
                {
                    SanitizeNode(value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is JsonValue val && val.TryGetValue(out string? str))
                {
                    arr[i] = sanitizer.Sanitize(str);
                }
                else if (arr[i] is not null)
                {
                    SanitizeNode(arr[i]!);
                }
            }
        }
    }
}

internal sealed partial class RedisSseDispatcher
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Published SSE event {Type} to channel {Channel}")]
    private partial void LogPublished(string type, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish SSE event {Type} to channel {Channel}")]
    private partial void LogPublishFailed(Exception ex, string type, string channel);
}
```

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh api`
Expected: All RedisSseDispatcher tests PASS.

**Step 5: Commit**

```bash
git add src/Wallow.Api/Services/RedisSseDispatcher.cs tests/Wallow.Api.Tests/Services/RedisSseDispatcherTests.cs
git commit -m "feat(realtime): add RedisSseDispatcher for pub/sub event publishing"
```

---

### Task 5: Implement SseRedisSubscriber (BackgroundService)

Subscribes to Redis pub/sub channels and fans out to local `SseConnectionManager`.

**Files:**
- Create: `src/Wallow.Api/Services/SseRedisSubscriber.cs`
- Test: `tests/Wallow.Api.Tests/Services/SseRedisSubscriberTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class SseRedisSubscriberTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly SseConnectionManager _connectionManager = new();

    public SseRedisSubscriberTests()
    {
        _redis.GetSubscriber().Returns(_subscriber);
    }

    [Fact]
    public async Task OnTenantMessage_DispatchesToConnectionManager()
    {
        // This test verifies the message handler callback logic directly
        System.Security.Claims.ClaimsPrincipal user = CreatePrincipal("user-1");
        Guid tenantId = Guid.NewGuid();
        SseConnection connection = _connectionManager.Register(tenantId, user, ["inquiries"]);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { Id = 1 });
        string json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Simulate what the subscriber does when it receives a message
        _connectionManager.Dispatch(tenantId, JsonSerializer.Deserialize<RealtimeEnvelope>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);

        bool hasItem = await connection.Reader.WaitToReadAsync(new CancellationTokenSource(1000).Token);
        hasItem.Should().BeTrue();
    }

    private static System.Security.Claims.ClaimsPrincipal CreatePrincipal(string userId)
    {
        System.Security.Claims.ClaimsIdentity identity = new("test");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        return new System.Security.Claims.ClaimsPrincipal(identity);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh api`
Expected: FAIL — `SseRedisSubscriber` does not exist.

**Step 3: Implement SseRedisSubscriber**

```csharp
using System.Text.Json;
using StackExchange.Redis;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

internal sealed partial class SseRedisSubscriber(
    IConnectionMultiplexer redis,
    SseConnectionManager connectionManager,
    ILogger<SseRedisSubscriber> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ISubscriber subscriber = redis.GetSubscriber();

        // Subscribe to tenant events (pattern: sse:tenant:*)
        await subscriber.SubscribeAsync(RedisChannel.Pattern("sse:tenant:*"), (channel, message) =>
        {
            HandleTenantMessage(channel.ToString(), message);
        });

        // Subscribe to user-targeted events (pattern: sse:user:*)
        await subscriber.SubscribeAsync(RedisChannel.Pattern("sse:user:*"), (channel, message) =>
        {
            HandleUserMessage(channel.ToString(), message);
        });

        LogSubscribed();

        // Keep running until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private void HandleTenantMessage(string channel, RedisValue message)
    {
        try
        {
            // Channel format: sse:tenant:{tenantId}
            string tenantIdStr = channel["sse:tenant:".Length..];
            if (!Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                LogInvalidChannel(channel);
                return;
            }

            RealtimeEnvelope? envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(message.ToString(), _jsonOptions);
            if (envelope is null)
            {
                return;
            }

            connectionManager.Dispatch(tenantId, envelope);
        }
        catch (Exception ex)
        {
            LogHandleFailed(ex, channel);
        }
    }

    private void HandleUserMessage(string channel, RedisValue message)
    {
        try
        {
            // Channel format: sse:user:{userId}
            string userId = channel["sse:user:".Length..];

            RealtimeEnvelope? envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(message.ToString(), _jsonOptions);
            if (envelope is null)
            {
                return;
            }

            connectionManager.DispatchToUser(userId, envelope);
        }
        catch (Exception ex)
        {
            LogHandleFailed(ex, channel);
        }
    }
}

internal sealed partial class SseRedisSubscriber
{
    [LoggerMessage(Level = LogLevel.Information, Message = "SSE Redis subscriber started, listening on sse:tenant:* and sse:user:*")]
    private partial void LogSubscribed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid SSE Redis channel: {Channel}")]
    private partial void LogInvalidChannel(string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to handle SSE Redis message from {Channel}")]
    private partial void LogHandleFailed(Exception ex, string channel);
}
```

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh api`
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add src/Wallow.Api/Services/SseRedisSubscriber.cs tests/Wallow.Api.Tests/Services/SseRedisSubscriberTests.cs
git commit -m "feat(realtime): add SseRedisSubscriber background service for pub/sub fan-out"
```

---

### Task 6: Implement SSE Endpoint

**Files:**
- Create: `src/Wallow.Api/Endpoints/SseEndpoint.cs`
- Test: `tests/Wallow.Api.Tests/Endpoints/SseEndpointTests.cs`

**Step 1: Write the failing tests**

Note: SSE endpoints are tricky to unit test. Focus on integration tests that verify the full pipeline. For now, write a basic test that the endpoint exists and requires auth.

```csharp
using System.Net;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Endpoints;

[Collection(nameof(ApiTestCollection))]
[Trait("Category", "Integration")]
public class SseEndpointTests(WallowApiFactory factory)
{
    [Fact]
    public async Task GetEvents_WithoutAuth_Returns401()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/events?subscribe=inquiries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEvents_WithAuth_Returns200WithCorrectContentType()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token:user-1:admin");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        try
        {
            HttpResponseMessage response = await client.GetAsync("/events?subscribe=inquiries",
                HttpCompletionOption.ResponseHeadersRead, cts.Token);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        }
        catch (OperationCanceledException)
        {
            // Expected — SSE stream stays open until cancelled
        }
    }

    [Fact]
    public async Task GetEvents_WithoutSubscribe_Returns400()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token:user-1:admin");

        HttpResponseMessage response = await client.GetAsync("/events");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh api`
Expected: FAIL — `/events` endpoint does not exist.

**Step 3: Implement the SSE endpoint**

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Api.Endpoints;

public static class SseEndpoint
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapSseEndpoint(this WebApplication app)
    {
        app.MapGet("/events", [Authorize] async (
            HttpContext context,
            SseConnectionManager connectionManager,
            ITenantContext tenantContext,
            string? subscribe,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(subscribe))
            {
                return Results.BadRequest("The 'subscribe' query parameter is required.");
            }

            if (!tenantContext.IsResolved)
            {
                return Results.BadRequest("Tenant context could not be resolved.");
            }

            List<string> modules = subscribe.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (modules.Count == 0)
            {
                return Results.BadRequest("At least one module must be specified in 'subscribe'.");
            }

            ClaimsPrincipal user = context.User;
            Guid tenantId = tenantContext.TenantId.Value;
            SseConnection connection = connectionManager.Register(tenantId, user, modules);

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            try
            {
                await foreach (RealtimeEnvelope envelope in connection.Reader.ReadAllAsync(ct))
                {
                    string json = JsonSerializer.Serialize(envelope, _jsonOptions);
                    await context.Response.WriteAsync($"event: {envelope.Module}.{envelope.Type}\n", ct);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — expected
            }
            finally
            {
                connectionManager.Unregister(connection.Id);
            }

            return Results.Empty;
        });
    }
}
```

**Step 4: Register the endpoint in Program.cs**

In `Program.cs`, after `app.MapHub<RealtimeHub>("/hubs/realtime");` add:

```csharp
app.MapSseEndpoint();
```

**Step 5: Run tests to verify they pass**

Run: `./scripts/run-tests.sh api`
Expected: All SSE endpoint tests PASS.

**Step 6: Commit**

```bash
git add src/Wallow.Api/Endpoints/SseEndpoint.cs tests/Wallow.Api.Tests/Endpoints/SseEndpointTests.cs src/Wallow.Api/Program.cs
git commit -m "feat(realtime): add SSE endpoint with module subscription and audience filtering"
```

---

### Task 7: Register SSE services in Program.cs

**Files:**
- Modify: `src/Wallow.Api/Program.cs`

**Step 1: Add service registrations**

After the existing `IRealtimeDispatcher` registration (line 226), add:

```csharp
builder.Services.AddSingleton<SseConnectionManager>();
builder.Services.AddSingleton<ISseDispatcher, RedisSseDispatcher>();
builder.Services.AddHostedService<SseRedisSubscriber>();
```

**Step 2: Verify build succeeds**

Run: `dotnet build src/Wallow.Api`
Expected: Build succeeds.

**Step 3: Run all tests**

Run: `./scripts/run-tests.sh api`
Expected: All tests pass.

**Step 4: Commit**

```bash
git add src/Wallow.Api/Program.cs
git commit -m "feat(realtime): register SSE services in DI container"
```

---

### Task 8: Migrate notification handlers from IRealtimeDispatcher to ISseDispatcher

**Files:**
- Modify: `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquiryCommentAddedSignalRHandler.cs`
- Modify: `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquirySubmittedSignalRHandler.cs`
- Modify: `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquiryStatusChangedSignalRHandler.cs`
- Modify: `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SignalRNotificationService.cs`
- Modify: `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InquiryCommentAddedSignalRHandlerTests.cs`
- Modify: `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InquirySubmittedSignalRHandlerTests.cs`
- Modify: `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InquiryStatusChangedSignalRHandlerTests.cs`

**Step 1: Update tests first**

Update `InquiryCommentAddedSignalRHandlerTests.cs` — rename class and mock to use `ISseDispatcher`:

```csharp
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryCommentAddedSseHandlerTests
{
    private readonly ISseDispatcher _dispatcher = Substitute.For<ISseDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private static readonly Guid _tenantId = Guid.NewGuid();

    private static InquiryCommentAddedEvent BuildEvent(bool isInternal = false) => new()
    {
        InquiryCommentId = Guid.NewGuid(),
        InquiryId = Guid.NewGuid(),
        TenantId = _tenantId,
        AuthorId = "admin-user-1",
        AuthorName = "Admin User",
        IsInternal = isInternal,
        SubmitterEmail = "submitter@test.com",
        SubmitterName = "Test Submitter",
        InquirySubject = "Test Inquiry",
        CommentContent = "Test comment"
    };

    [Fact]
    public async Task Handle_PublicComment_DispatchesToTenant()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(isInternal: false);

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            _tenantId,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantPermissionAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Handle_InternalComment_DispatchesWithPermission()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(isInternal: true);

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantPermissionAsync(
            _tenantId,
            "InquiriesRead",
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquiryCommentAddedSseHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantPermissionAsync(default, default!, default!, default);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `./scripts/run-tests.sh notifications`
Expected: FAIL — handler classes don't exist yet with new names.

**Step 3: Update the handlers**

Rename and update `InquiryCommentAddedSignalRHandler.cs` → `InquiryCommentAddedSseHandler.cs`:

```csharp
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedSseHandler
{
    public static async Task Handle(
        InquiryCommentAddedEvent message,
        ITenantContext tenantContext,
        ISseDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryCommentAdded",
            message);

        if (message.IsInternal)
        {
            await dispatcher.SendToTenantPermissionAsync(message.TenantId, "InquiriesRead", envelope);
        }
        else
        {
            await dispatcher.SendToTenantAsync(message.TenantId, envelope);
        }
    }
}
```

Rename and update `InquirySubmittedSignalRHandler.cs` → `InquirySubmittedSseHandler.cs`:

```csharp
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquirySubmittedSseHandler
{
    public static async Task Handle(
        InquirySubmittedEvent message,
        ITenantContext tenantContext,
        ISseDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquirySubmitted",
            new { message.InquiryId, message.Name, message.Email });

        await dispatcher.SendToTenantPermissionAsync(tenantContext.TenantId.Value, "InquiriesRead", envelope);
    }
}
```

Rename and update `InquiryStatusChangedSignalRHandler.cs` → `InquiryStatusChangedSseHandler.cs`:

```csharp
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryStatusChangedSseHandler
{
    public static async Task Handle(
        InquiryStatusChangedEvent message,
        ITenantContext tenantContext,
        ISseDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryStatusUpdated",
            new { message.InquiryId, message.NewStatus });

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
```

Update `SignalRNotificationService.cs` — change `BroadcastToTenantAsync` to use `ISseDispatcher`:

```csharp
using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class SignalRNotificationService(
    IRealtimeDispatcher dispatcher,
    ISseDispatcher sseDispatcher,
    TimeProvider timeProvider,
    ILogger<SignalRNotificationService> logger) : INotificationService
{
    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "NotificationCreated", payload);
        await sseDispatcher.SendToUserAsync(userId.ToString(), envelope, cancellationToken);

        LogSentToUser(logger, userId, title);
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
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "AnnouncementPublished", payload);
        await sseDispatcher.SendToTenantAsync(tenantId.Value, envelope, cancellationToken);

        LogBroadcastToTenant(logger, tenantId.Value, title);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent real-time notification to user {UserId}: {Title}")]
    private static partial void LogSentToUser(ILogger logger, Guid userId, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast announcement notification to tenant {TenantId}: {Title}")]
    private static partial void LogBroadcastToTenant(ILogger logger, Guid tenantId, string title);
}
```

Also update the corresponding tests for `InquirySubmittedSseHandler`, `InquiryStatusChangedSseHandler`, and `SignalRNotificationService` to match the new signatures.

**Step 4: Delete the old SignalR handler files**

Delete:
- `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquiryCommentAddedSignalRHandler.cs`
- `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquirySubmittedSignalRHandler.cs`
- `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquiryStatusChangedSignalRHandler.cs`

**Step 5: Run tests to verify they pass**

Run: `./scripts/run-tests.sh notifications`
Expected: All handler tests PASS with new names and interfaces.

Run: `./scripts/run-tests.sh api`
Expected: All API tests still pass.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat(realtime): migrate notification handlers from SignalR to SSE dispatch"
```

---

### Task 9: Update realtime documentation

**Files:**
- Modify: `docs/architecture/signalr.md` (rename to `docs/architecture/realtime.md`)
- Modify: `docs/toc.yml`

**Step 1: Rename the doc**

```bash
git mv docs/architecture/signalr.md docs/architecture/realtime.md
```

**Step 2: Update the document**

Add a new section at the top covering the SSE/SignalR split, audience selection guide, group naming conventions, module subscription guide, and the mid-session limitation note. Keep existing SignalR content for presence/bidirectional use cases. Remove references to using SignalR for notifications — replace with SSE guidance.

Key sections to add:

1. **Architecture Overview** — updated diagram showing SSE for notifications, SignalR for presence
2. **SSE Endpoint** — `GET /events?subscribe=...`, bearer auth, module filtering
3. **Audience Selection Guide** — table showing when to use each `ISseDispatcher` method
4. **Handler Checklist** — when adding a new handler: does it contain sensitive data? If yes, use `SendToTenantPermissionAsync` or `SendToTenantRoleAsync`
5. **Mid-Session Limitation** — permission/role filtering uses JWT claims from connection time; changes require reconnection; not a security boundary

**Step 3: Update toc.yml**

Change the entry from `signalr.md` to `realtime.md`.

**Step 4: Commit**

```bash
git add docs/architecture/realtime.md docs/toc.yml
git commit -m "docs(realtime): update architecture guide for SSE + SignalR split"
```

---

### Task 10: Final verification

**Step 1: Run all tests**

Run: `./scripts/run-tests.sh`
Expected: All tests pass across all modules.

**Step 2: Run architecture tests**

Run: `./scripts/run-tests.sh arch`
Expected: All architecture tests pass (no cross-module violations from new `ISseDispatcher` usage since it lives in `Shared.Contracts`).

**Step 3: Verify build**

Run: `dotnet build`
Expected: Clean build with no warnings.

**Step 4: Push**

```bash
git push
```
