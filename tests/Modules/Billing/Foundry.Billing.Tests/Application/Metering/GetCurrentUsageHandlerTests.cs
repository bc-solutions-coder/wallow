using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Application.Metering.Queries.GetCurrentUsage;
using Foundry.Billing.Application.Metering.Services;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Metering;

public class GetCurrentUsageHandlerTests
{
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly IMeteringService _meteringService;
    private readonly GetCurrentUsageHandler _handler;

    public GetCurrentUsageHandlerTests()
    {
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _meteringService = Substitute.For<IMeteringService>();
        _handler = new GetCurrentUsageHandler(_meterRepository, _meteringService);
    }

    [Fact]
    public async Task Handle_WithNoMeterCodeFilter_ReturnsAllMeters()
    {
        GetCurrentUsageQuery query = new(null, QuotaPeriod.Monthly);

        MeterDefinition meter1 = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        MeterDefinition meter2 = MeterDefinition.Create("storage.bytes", "Storage", "bytes", MeterAggregation.Max, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter1, meter2 });

        _meteringService.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly)
            .Returns(500);
        _meteringService.GetCurrentUsageAsync("storage.bytes", QuotaPeriod.Monthly)
            .Returns(1024);

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 500, 1000, 50, null));
        _meteringService.CheckQuotaAsync("storage.bytes")
            .Returns(new QuotaCheckResult(true, 1024, decimal.MaxValue, 0, null));

        Result<IReadOnlyList<UsageSummaryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(u => u.MeterCode == "api.calls" && u.CurrentValue == 500);
        result.Value.Should().Contain(u => u.MeterCode == "storage.bytes" && u.CurrentValue == 1024);
    }

    [Fact]
    public async Task Handle_WithMeterCodeFilter_ReturnsOnlyMatchingMeter()
    {
        GetCurrentUsageQuery query = new("api.calls", QuotaPeriod.Monthly);

        MeterDefinition meter1 = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        MeterDefinition meter2 = MeterDefinition.Create("storage.bytes", "Storage", "bytes", MeterAggregation.Max, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter1, meter2 });

        _meteringService.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly)
            .Returns(500);

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 500, 1000, 50, null));

        Result<IReadOnlyList<UsageSummaryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].MeterCode.Should().Be("api.calls");
    }

    [Fact]
    public async Task Handle_WithUnlimitedQuota_ReturnsNullLimit()
    {
        GetCurrentUsageQuery query = new("api.calls", QuotaPeriod.Monthly);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly)
            .Returns(500);

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 500, decimal.MaxValue, 0, null));

        Result<IReadOnlyList<UsageSummaryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Limit.Should().BeNull();
        result.Value[0].PercentUsed.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithQuotaLimit_ReturnsLimitAndPercent()
    {
        GetCurrentUsageQuery query = new("api.calls");

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.GetCurrentUsageAsync("api.calls", QuotaPeriod.Monthly)
            .Returns(750);

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 750, 1000, 75, null));

        Result<IReadOnlyList<UsageSummaryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Limit.Should().Be(1000);
        result.Value[0].PercentUsed.Should().Be(75);
    }
}
