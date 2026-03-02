using System.Net;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Infrastructure.Jobs;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Tests.Infrastructure.Metering;

public class FlushUsageJobExceptionTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContextFactory _tenantContextFactory;
    private readonly ILogger<FlushUsageJob> _logger;
    private readonly IServer _server;
    private readonly IDatabase _database;
    private readonly FlushUsageJob _job;

    public FlushUsageJobExceptionTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _tenantContextFactory = Substitute.For<ITenantContextFactory>();
        _logger = Substitute.For<ILogger<FlushUsageJob>>();
        _server = Substitute.For<IServer>();
        _database = Substitute.For<IDatabase>();

        IPEndPoint endpoint = new(IPAddress.Loopback, 6379);
        _redis.GetEndPoints(Arg.Any<bool>()).Returns(new EndPoint[] { endpoint });
        _redis.GetServer(endpoint, Arg.Any<object>()).Returns(_server);
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        _tenantContextFactory.CreateScope(Arg.Any<TenantId>()).Returns(Substitute.For<IDisposable>());

        _job = new FlushUsageJob(_redis, _usageRepository, _messageBus, _tenantContextFactory, TimeProvider.System, _logger);
    }

    [Fact]
    public async Task Execute_WhenRedisThrows_RethrowsException()
    {
        _redis.GetEndPoints(Arg.Any<bool>()).Returns(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        Func<Task> act = () => _job.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    [Fact]
    public async Task Execute_WhenProcessKeyThrowsException_ContinuesProcessing()
    {
        Guid tenantId1 = Guid.NewGuid();
        Guid tenantId2 = Guid.NewGuid();
        RedisKey[] keys = new[]
        {
            (RedisKey)$"meter:{tenantId1}:api.calls:2024-02",
            (RedisKey)$"meter:{tenantId2}:api.calls:2024-02"
        };

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable(keys));

        // First key throws, second key succeeds
        int callCount = 0;
        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new RedisTimeoutException("Timeout", CommandStatus.Unknown);
                }

                return new RedisValue("50");
            });

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        // Second key should still be processed despite first key failing
        _usageRepository.Received(1).Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithNegativeRedisValue_SkipsKey()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("-5"));

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
        _usageRepository.DidNotReceive().Update(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithKeyMissingPrefix_SkipsKey()
    {
        // Key has 4 parts but doesn't start with "meter"
        string key = "other:00000000-0000-0000-0000-000000000001:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        await _job.ExecuteAsync(CancellationToken.None);

        await _database.DidNotReceive().StringGetSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ParsePeriod_WithFivePartFormat_ThrowsArgumentException()
    {
        System.Reflection.MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Action act = () => method!.Invoke(null, new object[] { "2024-02-06-14-30" });

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*Invalid period format*");
    }

    private static async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(params RedisKey[] keys)
    {
        foreach (RedisKey key in keys)
        {
            yield return key;
        }

        await Task.CompletedTask;
    }
}
