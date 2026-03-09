using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Domain.Metering.Events;
using Foundry.Billing.Infrastructure.Services;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Tests.Infrastructure.Services;

public class ValkeyMeteringServiceAdditionalTests
{
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly IMessageBus _messageBus;
    private readonly ISubscriptionQueryService _subscriptionQueryService;
    private readonly IDatabase _database;
    private readonly ValkeyMeteringService _service;
    private readonly TenantId _tenantId;

    public ValkeyMeteringServiceAdditionalTests()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        _quotaRepository = Substitute.For<IQuotaDefinitionRepository>();
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _subscriptionQueryService = Substitute.For<ISubscriptionQueryService>();
        _database = Substitute.For<IDatabase>();

        _tenantId = TenantId.Create(Guid.NewGuid());
        tenantContext.TenantId.Returns(_tenantId);
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        ILogger<ValkeyMeteringService> logger = Substitute.For<ILogger<ValkeyMeteringService>>();
        _service = new ValkeyMeteringService(redis, tenantContext, _quotaRepository, _usageRepository, _meterRepository, _messageBus, _subscriptionQueryService, logger);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_ReturnsMappedDtos()
    {
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        UsageRecord record = UsageRecord.Create(_tenantId, "api.calls", from, to, 500, TimeProvider.System);

        _usageRepository.GetHistoryAsync("api.calls", from, to, Arg.Any<CancellationToken>())
            .Returns(new List<UsageRecord> { record });

        IReadOnlyList<UsageRecordDto> result = await _service.GetUsageHistoryAsync("api.calls", from, to);

        result.Should().HaveCount(1);
        result[0].MeterCode.Should().Be("api.calls");
        result[0].Value.Should().Be(500);
        result[0].TenantId.Should().Be(_tenantId.Value);
        result[0].PeriodStart.Should().Be(from);
        result[0].PeriodEnd.Should().Be(to);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_WithNoRecords_ReturnsEmptyList()
    {
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        _usageRepository.GetHistoryAsync("api.calls", from, to, Arg.Any<CancellationToken>())
            .Returns(new List<UsageRecord>());

        IReadOnlyList<UsageRecordDto> result = await _service.GetUsageHistoryAsync("api.calls", from, to);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenThresholdCrossed_PublishesThresholdEvent()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 100, QuotaPeriod.Monthly, QuotaAction.Warn);

        _subscriptionQueryService.GetActivePlanCodeAsync(_tenantId.Value, Arg.Any<CancellationToken>())
            .Returns("free");

        _quotaRepository.GetEffectiveQuotaAsync("api.calls", "free", Arg.Any<CancellationToken>())
            .Returns(quota);

        _database.StringGetAsync(
                Arg.Is<RedisKey>(k => k.ToString().Contains("api.calls") && !k.ToString().Contains("threshold")),
                Arg.Any<CommandFlags>())
            .Returns(new RedisValue("85"));

        // No previous threshold triggered
        _database.StringGetAsync(
                Arg.Is<RedisKey>(k => k.ToString().Contains("threshold")),
                Arg.Any<CommandFlags>())
            .Returns(new RedisValue("0"));

        MeterDefinition meter = MeterDefinition.Create(
            "api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        await _service.CheckQuotaAsync("api.calls");

        await _messageBus.Received().PublishAsync(
            Arg.Is<QuotaThresholdReachedEvent>(e =>
                e.MeterCode == "api.calls" &&
                e.PercentUsed == 80));
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenBelowAllThresholds_DoesNotPublishEvent()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 1000, QuotaPeriod.Monthly, QuotaAction.Warn);

        _subscriptionQueryService.GetActivePlanCodeAsync(_tenantId.Value, Arg.Any<CancellationToken>())
            .Returns("free");

        _quotaRepository.GetEffectiveQuotaAsync("api.calls", "free", Arg.Any<CancellationToken>())
            .Returns(quota);

        // 50% usage - below all thresholds
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("500"));

        await _service.CheckQuotaAsync("api.calls");

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<QuotaThresholdReachedEvent>());
    }

    [Fact]
    public async Task CheckQuotaAsync_UsesSubscriptionPlanCode()
    {
        _subscriptionQueryService.GetActivePlanCodeAsync(_tenantId.Value, Arg.Any<CancellationToken>())
            .Returns("enterprise");

        _quotaRepository.GetEffectiveQuotaAsync("api.calls", "enterprise", Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        await _service.CheckQuotaAsync("api.calls");

        await _subscriptionQueryService.Received(1)
            .GetActivePlanCodeAsync(_tenantId.Value, Arg.Any<CancellationToken>());
        await _quotaRepository.Received(1)
            .GetEffectiveQuotaAsync("api.calls", "enterprise", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncrementAsync_WithCustomValue_IncrementsCorrectAmount()
    {
        await _service.IncrementAsync("storage.bytes", 1024);

        await _database.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("storage.bytes")),
            (double)1024,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WithDailyPeriod_UsesCorrectKeyFormat()
    {
        DateTime now = DateTime.UtcNow;
        string expectedPeriod = now.ToString("yyyy-MM-dd");

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("100"));

        await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Daily);

        await _database.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(expectedPeriod)),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WithHourlyPeriod_UsesCorrectKeyFormat()
    {
        DateTime now = DateTime.UtcNow;
        string expectedPeriod = now.ToString("yyyy-MM-dd-HH");

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("42"));

        decimal result = await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Hourly);

        result.Should().Be(42);
        await _database.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(expectedPeriod)),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WithUnknownPeriod_FallsBackToMonthlyFormat()
    {
        DateTime now = DateTime.UtcNow;
        string expectedPeriod = now.ToString("yyyy-MM");

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("77"));

        decimal result = await _service.GetCurrentUsageAsync("api.calls", (QuotaPeriod)999);

        result.Should().Be(77);
        await _database.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(expectedPeriod)),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WithMonthlyPeriod_UsesCorrectKeyFormat()
    {
        DateTime now = DateTime.UtcNow;
        string expectedPeriod = now.ToString("yyyy-MM");

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("200"));

        decimal result = await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly);

        result.Should().Be(200);
        await _database.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(expectedPeriod)),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetCurrentUsageAsync_WhenNoValue_ReturnsZero()
    {
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        decimal result = await _service.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly);

        result.Should().Be(0);
    }
}
