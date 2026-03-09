using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Application.Metering.Queries.GetQuotaStatus;
using Foundry.Billing.Application.Metering.Services;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Metering;

public class GetQuotaStatusHandlerTests
{
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly IMeteringService _meteringService;
    private readonly GetQuotaStatusHandler _handler;

    public GetQuotaStatusHandlerTests()
    {
        _quotaRepository = Substitute.For<IQuotaDefinitionRepository>();
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _meteringService = Substitute.For<IMeteringService>();
        _handler = new GetQuotaStatusHandler(_quotaRepository, _meterRepository, _meteringService);
    }

    [Fact]
    public async Task Handle_WithQuotaLimitedMeters_ReturnsQuotaStatus()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 750, 1000, 75, null));

        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].MeterCode.Should().Be("api.calls");
        result.Value[0].MeterDisplayName.Should().Be("API Calls");
        result.Value[0].CurrentUsage.Should().Be(750);
        result.Value[0].Limit.Should().Be(1000);
        result.Value[0].PercentUsed.Should().Be(75);
        result.Value[0].Period.Should().Be("Monthly");
        result.Value[0].IsOverride.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithUnlimitedMeter_SkipsMeterInResults()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(QuotaCheckResult.Unlimited);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithTenantOverride_SetsIsOverrideTrue()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 500, 5000, 10, null));

        QuotaDefinition tenantOverride = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            TenantId.Create(Guid.NewGuid()),
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(tenantOverride);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].IsOverride.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithExceededQuota_ReturnsOnExceededAction()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(false, 1200, 1000, 120, QuotaAction.Block));

        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].OnExceeded.Should().Be("Block");
        result.Value[0].PercentUsed.Should().Be(120);
    }

    [Fact]
    public async Task Handle_WithNoMeters_ReturnsEmptyList()
    {
        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMultipleMeters_MixesLimitedAndUnlimited()
    {
        MeterDefinition meter1 = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        MeterDefinition meter2 = MeterDefinition.Create("storage.bytes", "Storage", "bytes", MeterAggregation.Max, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter1, meter2 });

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(true, 500, 1000, 50, null));
        _meteringService.CheckQuotaAsync("storage.bytes")
            .Returns(QuotaCheckResult.Unlimited);

        _quotaRepository.GetTenantOverrideAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result<IReadOnlyList<QuotaStatusDto>> result = await _handler.Handle(
            new GetQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].MeterCode.Should().Be("api.calls");
    }
}
