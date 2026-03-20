using Wallow.Billing.Application.Metering.Commands.SetQuotaOverride;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Metering;

public class SetQuotaOverrideHandlerTests
{
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly SetQuotaOverrideHandler _handler;

    public SetQuotaOverrideHandlerTests()
    {
        _quotaRepository = Substitute.For<IQuotaDefinitionRepository>();
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _handler = new SetQuotaOverrideHandler(_quotaRepository, _meterRepository);
    }

    [Fact]
    public async Task Handle_WhenMeterNotFound_ReturnsNotFoundError()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns((MeterDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenNoExistingOverride_CreatesNewQuota()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideCommand command = new(
            tenantId,
            "api.calls",
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Warn);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Add(Arg.Is<QuotaDefinition>(q =>
            q.MeterCode == "api.calls" &&
            q.TenantId.Value == tenantId &&
            q.Limit == 5000 &&
            q.Period == QuotaPeriod.Monthly &&
            q.OnExceeded == QuotaAction.Warn));
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingOverride_UpdatesQuota()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideCommand command = new(
            tenantId,
            "api.calls",
            10000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        QuotaDefinition existingQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            TenantId.Create(tenantId),
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Warn);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns(existingQuota);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existingQuota.Limit.Should().Be(10000);
        existingQuota.OnExceeded.Should().Be(QuotaAction.Block);
        _quotaRepository.DidNotReceive().Add(Arg.Any<QuotaDefinition>());
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExecutedTwiceWithSameCommand_IsIdempotent()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideCommand command = new(
            tenantId,
            "api.calls",
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Warn);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        QuotaDefinition? capturedQuota = null;
        _quotaRepository.Add(Arg.Do<QuotaDefinition>(q => capturedQuota = q));

        Result result1 = await _handler.Handle(command, CancellationToken.None);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns(capturedQuota);

        Result result2 = await _handler.Handle(command, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Add(Arg.Any<QuotaDefinition>());
        capturedQuota.Should().NotBeNull();
        capturedQuota!.Limit.Should().Be(5000);
    }

    [Fact]
    public async Task Handle_WhenExecutedConcurrentlyForDifferentTenants_CreatesSeparateQuotas()
    {
        Guid tenant1 = Guid.NewGuid();
        Guid tenant2 = Guid.NewGuid();
        Guid tenant3 = Guid.NewGuid();

        SetQuotaOverrideCommand command1 = new(tenant1, "api.calls", 1000, QuotaPeriod.Monthly, QuotaAction.Block);
        SetQuotaOverrideCommand command2 = new(tenant2, "api.calls", 2000, QuotaPeriod.Monthly, QuotaAction.Warn);
        SetQuotaOverrideCommand command3 = new(tenant3, "api.calls", 3000, QuotaPeriod.Monthly, QuotaAction.Block);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Task<Result>[] tasks = new[]
        {
            _handler.Handle(command1, CancellationToken.None),
            _handler.Handle(command2, CancellationToken.None),
            _handler.Handle(command3, CancellationToken.None)
        };
        Result[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        _quotaRepository.Received(3).Add(Arg.Any<QuotaDefinition>());
        await _quotaRepository.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExecutedConcurrentlyForSameTenant_LastWriteWins()
    {
        Guid tenantId = Guid.NewGuid();

        SetQuotaOverrideCommand command1 = new(tenantId, "api.calls", 1000, QuotaPeriod.Monthly, QuotaAction.Block);
        SetQuotaOverrideCommand command2 = new(tenantId, "api.calls", 2000, QuotaPeriod.Monthly, QuotaAction.Warn);
        SetQuotaOverrideCommand command3 = new(tenantId, "api.calls", 3000, QuotaPeriod.Daily, QuotaAction.Block);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Task<Result>[] tasks = new[]
        {
            _handler.Handle(command1, CancellationToken.None),
            _handler.Handle(command2, CancellationToken.None),
            _handler.Handle(command3, CancellationToken.None)
        };
        Result[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        _quotaRepository.Received(3).Add(Arg.Any<QuotaDefinition>());
    }
}
