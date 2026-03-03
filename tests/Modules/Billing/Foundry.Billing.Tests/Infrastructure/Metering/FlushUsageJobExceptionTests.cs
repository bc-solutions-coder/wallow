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
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IDatabase _database;
    private readonly FlushUsageJob _job;

    public FlushUsageJobExceptionTests()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        ITenantContextFactory tenantContextFactory = Substitute.For<ITenantContextFactory>();
        ILogger<FlushUsageJob> logger = Substitute.For<ILogger<FlushUsageJob>>();
        _database = Substitute.For<IDatabase>();

        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        tenantContextFactory.CreateScope(Arg.Any<TenantId>()).Returns(Substitute.For<IDisposable>());

        _job = new FlushUsageJob(redis, _usageRepository, messageBus, tenantContextFactory, TimeProvider.System, logger);
    }

    [Fact]
    public async Task Execute_WhenRedisThrows_RethrowsException()
    {
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<RedisValue[]>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        Func<Task> act = () => _job.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    [Fact]
    public async Task Execute_WhenProcessKeyThrowsException_ContinuesProcessing()
    {
        Guid tenantId1 = Guid.NewGuid();
        Guid tenantId2 = Guid.NewGuid();
        RedisValue[] members = new RedisValue[]
        {
            $"meter:{tenantId1}:api.calls:2024-02",
            $"meter:{tenantId2}:api.calls:2024-02"
        };

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(members);

        // First key throws, second key succeeds
        int callCount = 0;
        _database.StringGetSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(_ =>
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

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { key });

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

        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { key });

        await _job.ExecuteAsync(CancellationToken.None);

        await _database.DidNotReceive().StringGetSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public void ParsePeriod_WithFivePartFormat_ThrowsArgumentException()
    {
        System.Reflection.MethodInfo? method = typeof(FlushUsageJob).GetMethod("ParsePeriod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Action act = () => method!.Invoke(null, new object[] { "2024-02-06-14-30" });

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*Invalid period format*");
    }
}
