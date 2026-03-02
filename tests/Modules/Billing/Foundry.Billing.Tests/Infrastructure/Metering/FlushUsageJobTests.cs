using System.Net;
using System.Reflection;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Events;
using Foundry.Billing.Infrastructure.Jobs;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Tests.Infrastructure.Metering;

public class FlushUsageJobTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContextFactory _tenantContextFactory;
    private readonly ILogger<FlushUsageJob> _logger;
    private readonly IServer _server;
    private readonly IDatabase _database;
    private readonly FlushUsageJob _job;

    public FlushUsageJobTests()
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
    public async Task Execute_ShouldFlushAllMeterKeys()
    {
        Guid tenantId = Guid.NewGuid();
        RedisKey[] keys = new[]
        {
            (RedisKey)$"meter:{tenantId}:api.calls:2024-02",
            (RedisKey)$"meter:{tenantId}:storage.bytes:2024-02",
            (RedisKey)$"meter:{tenantId}:emails.sent:2024-02"
        };

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable(keys));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("100"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.Received(3).Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_ShouldUseAtomicGetAndSet()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("500"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        await _database.Received(1).StringGetSetAsync(
            Arg.Is<RedisKey>(k => k.ToString() == key),
            0,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Execute_ShouldSkipZeroValues()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("0"));

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_ShouldPublishUsageFlushedEvent()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("100"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UsageFlushedEvent>(e => e.RecordCount == 1));
    }

    [Fact]
    public async Task Execute_WhenExistingRecord_ShouldUpdateValue()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        string key = $"meter:{tenantId.Value}:api.calls:2024-02";

        _server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(ToAsyncEnumerable((RedisKey)key));

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("100"));

        UsageRecord existingRecord = UsageRecord.Create(tenantId, "api.calls", new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), 500, TimeProvider.System);

        _usageRepository.GetForPeriodAsync(
                "api.calls",
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(existingRecord);

        await _job.ExecuteAsync(CancellationToken.None);

        existingRecord.Value.Should().Be(600);
        _usageRepository.Received(1).Update(existingRecord);
        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    private static async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(
        params RedisKey[] keys)
    {
        foreach (RedisKey key in keys)
        {
            yield return key;
        }

        await Task.CompletedTask;
    }
}

public class ParsePeriodTests
{
    [Theory]
    [InlineData("2024-02", "2024-02-01T00:00:00Z", "2024-03-01T00:00:00Z")]
    [InlineData("2024-12", "2024-12-01T00:00:00Z", "2025-01-01T00:00:00Z")]
    public void ParsePeriod_Monthly_ShouldParseCorrectly(string input, string expectedStart, string expectedEnd)
    {
        MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        (DateTime, DateTime)? result = method!.Invoke(null, new object[] { input }) as (DateTime, DateTime)?;

        result.Should().NotBeNull();
        result!.Value.Item1.Should().Be(DateTime.Parse(expectedStart).ToUniversalTime());
        result.Value.Item2.Should().Be(DateTime.Parse(expectedEnd).ToUniversalTime());
    }

    [Theory]
    [InlineData("2024-02-06", "2024-02-06T00:00:00Z", "2024-02-07T00:00:00Z")]
    [InlineData("2024-12-31", "2024-12-31T00:00:00Z", "2025-01-01T00:00:00Z")]
    public void ParsePeriod_Daily_ShouldParseCorrectly(string input, string expectedStart, string expectedEnd)
    {
        MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        (DateTime, DateTime)? result = method!.Invoke(null, new object[] { input }) as (DateTime, DateTime)?;

        result.Should().NotBeNull();
        result!.Value.Item1.Should().Be(DateTime.Parse(expectedStart).ToUniversalTime());
        result.Value.Item2.Should().Be(DateTime.Parse(expectedEnd).ToUniversalTime());
    }

    [Theory]
    [InlineData("2024-02-06-14", "2024-02-06T14:00:00Z", "2024-02-06T15:00:00Z")]
    [InlineData("2024-12-31-23", "2024-12-31T23:00:00Z", "2025-01-01T00:00:00Z")]
    public void ParsePeriod_Hourly_ShouldParseCorrectly(string input, string expectedStart, string expectedEnd)
    {
        MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        (DateTime, DateTime)? result = method!.Invoke(null, new object[] { input }) as (DateTime, DateTime)?;

        result.Should().NotBeNull();
        result!.Value.Item1.Should().Be(DateTime.Parse(expectedStart).ToUniversalTime());
        result.Value.Item2.Should().Be(DateTime.Parse(expectedEnd).ToUniversalTime());
    }

    [Fact]
    public void ParsePeriod_InvalidFormat_ShouldThrowArgumentException()
    {
        MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Action act = () => method!.Invoke(null, new object[] { "invalid" });

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>();
    }
}
