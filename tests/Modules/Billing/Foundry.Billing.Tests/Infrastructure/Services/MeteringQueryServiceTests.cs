using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Infrastructure.Services;
using Foundry.Shared.Contracts.Metering;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Tests.Infrastructure.Services;

public class MeteringQueryServiceTests
{
    private readonly IUsageRecordRepository _usageRecordRepository;
    private readonly IQuotaDefinitionRepository _quotaDefinitionRepository;
    private readonly MeteringQueryService _service;

    public MeteringQueryServiceTests()
    {
        _usageRecordRepository = Substitute.For<IUsageRecordRepository>();
        _quotaDefinitionRepository = Substitute.For<IQuotaDefinitionRepository>();
        _service = new MeteringQueryService(_usageRecordRepository, _quotaDefinitionRepository);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenNoQuotaDefined_ReturnsNull()
    {
        Guid tenantId = Guid.NewGuid();

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        QuotaStatus? result = await _service.CheckQuotaAsync(tenantId, "api.calls");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUsageUnderLimit_ReturnsNotExceeded()
    {
        Guid tenantId = Guid.NewGuid();
        TenantId tenantIdTyped = TenantId.Create(tenantId);
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 1000, QuotaPeriod.Monthly, QuotaAction.Block);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        List<UsageRecord> records = new()
        {
            UsageRecord.Create(tenantIdTyped, "api.calls", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 300, TimeProvider.System),
            UsageRecord.Create(tenantIdTyped, "api.calls", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 200, TimeProvider.System)
        };

        _usageRecordRepository.GetHistoryAsync(
                "api.calls", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(records);

        QuotaStatus? result = await _service.CheckQuotaAsync(tenantId, "api.calls");

        result.Should().NotBeNull();
        result!.MeterCode.Should().Be("api.calls");
        result.Used.Should().Be(500);
        result.Limit.Should().Be(1000);
        result.IsExceeded.Should().BeFalse();
        result.PercentUsed.Should().Be(50m);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUsageOverLimit_ReturnsExceeded()
    {
        Guid tenantId = Guid.NewGuid();
        TenantId tenantIdTyped = TenantId.Create(tenantId);
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 100, QuotaPeriod.Monthly, QuotaAction.Block);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        List<UsageRecord> records = new()
        {
            UsageRecord.Create(tenantIdTyped, "api.calls", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 150, TimeProvider.System)
        };

        _usageRecordRepository.GetHistoryAsync(
                "api.calls", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(records);

        QuotaStatus? result = await _service.CheckQuotaAsync(tenantId, "api.calls");

        result.Should().NotBeNull();
        result!.IsExceeded.Should().BeTrue();
        result.Used.Should().Be(150);
        result.Limit.Should().Be(100);
    }

    [Fact]
    public async Task CheckQuotaAsync_WithNoUsageRecords_ReturnsZeroUsage()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 1000, QuotaPeriod.Monthly, QuotaAction.Block);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        _usageRecordRepository.GetHistoryAsync(
                "api.calls", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsageRecord>());

        QuotaStatus? result = await _service.CheckQuotaAsync(tenantId, "api.calls");

        result.Should().NotBeNull();
        result!.Used.Should().Be(0);
        result.IsExceeded.Should().BeFalse();
        result.PercentUsed.Should().Be(0m);
    }

    [Fact]
    public async Task CheckQuotaAsync_WithDailyPeriod_QueriesCorrectDateRange()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 1000, QuotaPeriod.Daily, QuotaAction.Warn);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        _usageRecordRepository.GetHistoryAsync(
                "api.calls", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsageRecord>());

        await _service.CheckQuotaAsync(tenantId, "api.calls");

        await _usageRecordRepository.Received(1).GetHistoryAsync(
            "api.calls",
            Arg.Is<DateTime>(d => d.Hour == 0 && d.Minute == 0 && d.Second == 0),
            Arg.Is<DateTime>(d => d.Hour == 0 && d.Minute == 0 && d.Second == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckQuotaAsync_WithHourlyPeriod_QueriesCorrectDateRange()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 100, QuotaPeriod.Hourly, QuotaAction.Block);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        _usageRecordRepository.GetHistoryAsync(
                "api.calls", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsageRecord>());

        await _service.CheckQuotaAsync(tenantId, "api.calls");

        await _usageRecordRepository.Received(1).GetHistoryAsync(
            "api.calls",
            Arg.Is<DateTime>(d => d.Minute == 0 && d.Second == 0),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckQuotaAsync_WithUnknownQuotaPeriod_ThrowsArgumentOutOfRange()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls", "free", 1000, (QuotaPeriod)999, QuotaAction.Block);

        _quotaDefinitionRepository.GetEffectiveQuotaAsync("api.calls", null, Arg.Any<CancellationToken>())
            .Returns(quota);

        Func<Task> act = async () => await _service.CheckQuotaAsync(tenantId, "api.calls");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
