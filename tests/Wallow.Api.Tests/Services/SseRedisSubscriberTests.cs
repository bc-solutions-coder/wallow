using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize")]
[SuppressMessage("Design", "CA1063:Implement IDisposable Correctly")]
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
public sealed class SseRedisSubscriberTests : IDisposable
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _redisSubscriber = Substitute.For<ISubscriber>();
    private readonly SseConnectionManager _connectionManager = Substitute.For<SseConnectionManager>();
    private readonly SseRedisSubscriber _sut;

    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly string[] _singleConnection = ["conn-1"];

    private Action<RedisChannel, RedisValue>? _tenantCallback;

    public SseRedisSubscriberTests()
    {
        _redis.GetSubscriber().Returns(_redisSubscriber);

        _redisSubscriber
            .SubscribeAsync(
                Arg.Is<RedisChannel>(ch => ch.ToString() == "sse:tenant:*"),
                Arg.Any<Action<RedisChannel, RedisValue>>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => _tenantCallback = ci.Arg<Action<RedisChannel, RedisValue>>());

        _redisSubscriber
            .SubscribeAsync(
                Arg.Is<RedisChannel>(ch => ch.ToString() == "sse:user:*"),
                Arg.Any<Action<RedisChannel, RedisValue>>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.CompletedTask);

        _sut = new SseRedisSubscriber(
            _redis,
            _connectionManager,
            NullLogger<SseRedisSubscriber>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    private async Task StartSubscriberAsync()
    {
        using CancellationTokenSource cts = new();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);
    }

    [Fact]
    public async Task ExecuteAsync_TenantMessage_DeliversToMatchingConnection()
    {
        Channel<RealtimeEnvelope> channel = Channel.CreateUnbounded<RealtimeEnvelope>();
        SseConnectionState state = new(
            "user-1", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string>(),
            new HashSet<string>(),
            channel);

        _connectionManager.GetConnectionsForTenant(_tenantId).Returns(_singleConnection);
        _connectionManager.GetConnectionState("conn-1").Returns(state);
        _connectionManager.ShouldDeliver(state, Arg.Any<RealtimeEnvelope>(), "Notifications").Returns(true);

        await StartSubscriberAsync();

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "Alert", new { Message = "hello" });
        string json = JsonSerializer.Serialize(envelope);

        _tenantCallback.Should().NotBeNull("subscriber should have registered a tenant channel callback");
        _tenantCallback!.Invoke(new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal), json);

        bool hasMessage = channel.Reader.TryRead(out RealtimeEnvelope? delivered);
        hasMessage.Should().BeTrue();
        delivered!.Type.Should().Be("Alert");
        delivered.Module.Should().Be("Notifications");
    }

    [Fact]
    public async Task ExecuteAsync_TenantMessageWithNoConnections_DoesNotThrow()
    {
        _connectionManager.GetConnectionsForTenant(_tenantId).Returns(Array.Empty<string>());

        await StartSubscriberAsync();

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "Alert", new { });
        string json = JsonSerializer.Serialize(envelope);

        _tenantCallback.Should().NotBeNull();

        Action act = () => _tenantCallback!.Invoke(
            new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal), json);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeliverReturnsFalse_ChannelReceivesNoWrite()
    {
        Channel<RealtimeEnvelope> channel = Channel.CreateUnbounded<RealtimeEnvelope>();
        SseConnectionState state = new(
            "user-1", _tenantId,
            new HashSet<string> { "Billing" },
            new HashSet<string>(),
            new HashSet<string>(),
            channel);

        _connectionManager.GetConnectionsForTenant(_tenantId).Returns(_singleConnection);
        _connectionManager.GetConnectionState("conn-1").Returns(state);
        _connectionManager.ShouldDeliver(state, Arg.Any<RealtimeEnvelope>(), Arg.Any<string>()).Returns(false);

        await StartSubscriberAsync();

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Billing", "InvoiceCreated", new { });
        string json = JsonSerializer.Serialize(envelope);

        _tenantCallback.Should().NotBeNull();
        _tenantCallback!.Invoke(new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal), json);

        bool hasMessage = channel.Reader.TryRead(out _);
        hasMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MalformedJsonPayload_DoesNotCrashSubscriber()
    {
        _connectionManager.GetConnectionsForTenant(Arg.Any<Guid>()).Returns(_singleConnection);

        await StartSubscriberAsync();

        _tenantCallback.Should().NotBeNull();

        Action act = () => _tenantCallback!.Invoke(
            new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal),
            "not-valid-json{{{");

        act.Should().NotThrow();
    }
}
