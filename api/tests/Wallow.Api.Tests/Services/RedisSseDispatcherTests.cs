using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class RedisSseDispatcherTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly RedisSseDispatcher _sut;
    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public RedisSseDispatcherTests()
    {
        _redis.GetSubscriber().Returns(_subscriber);
        _sut = new RedisSseDispatcher(_redis, NullLogger<RedisSseDispatcher>.Instance);
    }

    [Fact]
    public async Task SendToTenantAsync_WithValidEnvelope_PublishesToTenantChannel()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { TaskId = 42 });

        await _sut.SendToTenantAsync(_tenantId, envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(ch => ch == new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal)),
            Arg.Is<RedisValue>(val => DeserializesBackToEnvelope(val, "Notifications", "TaskAssigned")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToUserAsync_WithValidEnvelope_PublishesToUserChannel()
    {
        string userId = "user-42";
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Billing", "InvoiceCreated", new { InvoiceId = 7 });

        await _sut.SendToUserAsync(userId, envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(ch => ch == new RedisChannel($"sse:user:{userId}", RedisChannel.PatternMode.Literal)),
            Arg.Is<RedisValue>(val => DeserializesBackToEnvelope(val, "Billing", "InvoiceCreated")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_WithValidEnvelope_PublishesToTenantChannelWithPermission()
    {
        string permission = "billing:read";
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Billing", "InvoiceCreated", new { InvoiceId = 1 });

        await _sut.SendToTenantPermissionAsync(_tenantId, permission, envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(ch => ch == new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal)),
            Arg.Is<RedisValue>(val => JsonContainsPermission(val, "billing:read")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantRoleAsync_WithValidEnvelope_PublishesToTenantChannelWithRole()
    {
        string role = "Admin";
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Identity", "RoleChanged", new { UserId = "u1" });

        await _sut.SendToTenantRoleAsync(_tenantId, role, envelope);

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(ch => ch == new RedisChannel($"sse:tenant:{_tenantId}", RedisChannel.PatternMode.Literal)),
            Arg.Is<RedisValue>(val => JsonContainsRole(val, "Admin")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendToTenantAsync_WhenRedisThrows_DoesNotRethrow()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Func<Task> act = () => _sut.SendToTenantAsync(_tenantId, envelope);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToUserAsync_WhenRedisThrows_DoesNotRethrow()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Func<Task> act = () => _sut.SendToUserAsync("user-1", envelope);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_WhenRedisThrows_DoesNotRethrow()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Func<Task> act = () => _sut.SendToTenantPermissionAsync(_tenantId, "perm", envelope);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToTenantRoleAsync_WhenRedisThrows_DoesNotRethrow()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Func<Task> act = () => _sut.SendToTenantRoleAsync(_tenantId, "Admin", envelope);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_SerializedJson_ContainsRequiredPermissionValue()
    {
        string permission = "inquiries:manage";
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Inquiries", "CommentAdded", new { InquiryId = 5 });
        RedisValue capturedValue = default;

        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(callInfo =>
            {
                capturedValue = callInfo.Arg<RedisValue>();
                return 1L;
            });

        await _sut.SendToTenantPermissionAsync(_tenantId, permission, envelope);

        string json = capturedValue.ToString();
        json.Should().Contain("inquiries:manage");
    }

    [Fact]
    public async Task SendToTenantAsync_SerializedJson_DeserializesToMatchingEnvelope()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "Alert", new { Message = "hello" });
        RedisValue capturedValue = default;

        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(callInfo =>
            {
                capturedValue = callInfo.Arg<RedisValue>();
                return 1L;
            });

        await _sut.SendToTenantAsync(_tenantId, envelope);

        RealtimeEnvelope? deserialized = JsonSerializer.Deserialize<RealtimeEnvelope>(capturedValue.ToString());
        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be("Alert");
        deserialized.Module.Should().Be("Notifications");
    }

    private static bool DeserializesBackToEnvelope(RedisValue value, string expectedModule, string expectedType)
    {
        try
        {
            RealtimeEnvelope? envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(value.ToString());
            return envelope is not null && envelope.Module == expectedModule && envelope.Type == expectedType;
        }
        catch
        {
            return false;
        }
    }

    private static bool JsonContainsPermission(RedisValue value, string permission)
    {
        string json = value.ToString();
        return json.Contains(permission, StringComparison.Ordinal);
    }

    private static bool JsonContainsRole(RedisValue value, string role)
    {
        string json = value.ToString();
        return json.Contains(role, StringComparison.Ordinal);
    }
}
