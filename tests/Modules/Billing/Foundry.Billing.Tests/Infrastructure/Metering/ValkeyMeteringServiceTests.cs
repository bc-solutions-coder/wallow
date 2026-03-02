using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Infrastructure.Services;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Tests.Infrastructure.Metering;

public class ValkeyMeteringServiceTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITenantContext _tenantContext;
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly IMessageBus _messageBus;
    private readonly ISubscriptionQueryService _subscriptionQueryService;
    private readonly IDatabase _database;
    private readonly ValkeyMeteringService _service;
    private readonly TenantId _testTenantId;

    public ValkeyMeteringServiceTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _tenantContext = Substitute.For<ITenantContext>();
        _quotaRepository = Substitute.For<IQuotaDefinitionRepository>();
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _subscriptionQueryService = Substitute.For<ISubscriptionQueryService>();
        _database = Substitute.For<IDatabase>();

        _testTenantId = TenantId.Create(Guid.NewGuid());
        _tenantContext.TenantId.Returns(_testTenantId);
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        ILogger<ValkeyMeteringService> logger = Substitute.For<ILogger<ValkeyMeteringService>>();
        _service = new ValkeyMeteringService(_redis, _tenantContext, _quotaRepository, _usageRepository, _meterRepository, _messageBus, _subscriptionQueryService, logger);
    }

    [Fact]
    public async Task IncrementAsync_ShouldIncrementCounter()
    {
        await _service.IncrementAsync("api.calls", 1);

        await _database.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("api.calls")),
            1,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IncrementAsync_ShouldSetExpiry()
    {
        await _service.IncrementAsync("api.calls", 1);

        await _database.Received(1).KeyExpireAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("api.calls")),
            TimeSpan.FromDays(90),
            ExpireWhen.HasNoExpiry,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IncrementAsync_ShouldUseCorrectKeyFormat()
    {
        DateTime now = DateTime.UtcNow;
        string expectedPeriod = now.ToString("yyyy-MM");
        string expectedKey = $"meter:{_testTenantId.Value}:api.calls:{expectedPeriod}";

        await _service.IncrementAsync("api.calls", 5);

        await _database.Received().StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString() == expectedKey),
            5,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUnderLimit_ShouldReturnAllowed()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetEffectiveQuotaAsync(
                "api.calls",
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(quota);

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("500"));

        QuotaCheckResult result = await _service.CheckQuotaAsync("api.calls");

        result.IsAllowed.Should().BeTrue();
        result.CurrentUsage.Should().Be(500);
        result.Limit.Should().Be(1000);
        result.PercentUsed.Should().Be(50);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenOverLimit_ShouldReturnBlocked()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetEffectiveQuotaAsync(
                "api.calls",
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(quota);

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("1001"));

        QuotaCheckResult result = await _service.CheckQuotaAsync("api.calls");

        result.IsAllowed.Should().BeFalse();
        result.CurrentUsage.Should().Be(1001);
        result.Limit.Should().Be(1000);
        result.PercentUsed.Should().Be(100.1m);
        result.ActionIfExceeded.Should().Be(QuotaAction.Block);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenNoQuotaDefined_ShouldReturnUnlimited()
    {
        _quotaRepository.GetEffectiveQuotaAsync(
                "api.calls",
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        QuotaCheckResult result = await _service.CheckQuotaAsync("api.calls");

        result.IsAllowed.Should().BeTrue();
        result.Limit.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenAtExactly100Percent_ShouldReturnNotAllowed()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetEffectiveQuotaAsync(
                "api.calls",
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(quota);

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("1000"));

        QuotaCheckResult result = await _service.CheckQuotaAsync("api.calls");

        result.IsAllowed.Should().BeFalse();
        result.PercentUsed.Should().Be(100);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_ShouldReturnCurrentValue()
    {
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("750"));

        decimal result = await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly);

        result.Should().Be(750);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WhenKeyDoesNotExist_ShouldReturnZero()
    {
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        decimal result = await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly);

        result.Should().Be(0);
    }
}
