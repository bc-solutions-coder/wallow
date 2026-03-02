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

public class FlushUsageJobAdditionalTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContextFactory _tenantContextFactory;
    private readonly ILogger<FlushUsageJob> _logger;
    private readonly IServer _server;
    private readonly IDatabase _database;
    private readonly FlushUsageJob _job;

    public FlushUsageJobAdditionalTests()
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
    public async Task Execute_WithInvalidKeyFormat_SkipsKey()
    {
        string invalidKey = "not-a-meter-key";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)invalidKey));

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
        _usageRepository.DidNotReceive().Update(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithInvalidTenantId_SkipsKey()
    {
        string invalidKey = "meter:not-a-guid:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)invalidKey));

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithNoKeys_DoesNotPublishEvent()
    {
        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable());

        await _job.ExecuteAsync(CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Execute_WithNullRedisValue_SkipsKey()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WhenCancelled_StopsProcessing()
    {
        Guid tenantId = Guid.NewGuid();
        RedisKey[] keys = new[]
        {
            (RedisKey)$"meter:{tenantId}:api.calls:2024-02",
            (RedisKey)$"meter:{tenantId}:storage.bytes:2024-02"
        };

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable(keys));

        using CancellationTokenSource cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _job.ExecuteAsync(cts.Token);

        await _database.DidNotReceive().StringGetSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Execute_CallsSaveChangesAsync()
    {
        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable());

        await _job.ExecuteAsync(CancellationToken.None);

        await _usageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithDailyPeriod_ParsesCorrectly()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02-15";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("50"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.Received(1).Add(Arg.Is<UsageRecord>(r => r.MeterCode == "api.calls"));
    }

    [Fact]
    public async Task Execute_WithHourlyPeriod_ParsesCorrectly()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02-15-10";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("75"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.Received(1).Add(Arg.Is<UsageRecord>(r => r.MeterCode == "api.calls"));
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
