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
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly IDatabase _database;
    private readonly FlushUsageJob _job;

    public FlushUsageJobAdditionalTests()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        ITenantContextFactory tenantContextFactory = Substitute.For<ITenantContextFactory>();
        ILogger<FlushUsageJob> logger = Substitute.For<ILogger<FlushUsageJob>>();
        _database = Substitute.For<IDatabase>();

        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        tenantContextFactory.CreateScope(Arg.Any<TenantId>()).Returns(Substitute.For<IDisposable>());

        _job = new FlushUsageJob(redis, _usageRepository, _messageBus, tenantContextFactory, TimeProvider.System, logger);
    }

    [Fact]
    public async Task Execute_WithInvalidKeyFormat_SkipsKey()
    {
        string invalidKey = "not-a-meter-key";

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([invalidKey]);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
        _usageRepository.DidNotReceive().Update(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithInvalidTenantId_SkipsKey()
    {
        string invalidKey = "meter:not-a-guid:api.calls:2024-02";

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([invalidKey]);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WithNoKeys_DoesNotPublishEvent()
    {
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([]);

        await _job.ExecuteAsync(CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Execute_WithNullRedisValue_SkipsKey()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02";

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([key]);

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.DidNotReceive().Add(Arg.Any<UsageRecord>());
    }

    [Fact]
    public async Task Execute_WhenCancelled_StopsProcessing()
    {
        Guid tenantId = Guid.NewGuid();
        RedisValue[] members = [
            $"meter:{tenantId}:api.calls:2024-02",
            $"meter:{tenantId}:storage.bytes:2024-02"
        ];

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(members);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await _job.ExecuteAsync(cts.Token);

        await _database.DidNotReceive().StringGetSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Execute_CallsSaveChangesAsync()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-01";

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([key]);
        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("10"));
        _usageRepository.GetForPeriodAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        await _usageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithDailyPeriod_ParsesCorrectly()
    {
        Guid tenantId = Guid.NewGuid();
        string key = $"meter:{tenantId}:api.calls:2024-02-15";

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([key]);

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

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([key]);

        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("75"));

        _usageRepository.GetForPeriodAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((UsageRecord?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        _usageRepository.Received(1).Add(Arg.Is<UsageRecord>(r => r.MeterCode == "api.calls"));
    }
}
