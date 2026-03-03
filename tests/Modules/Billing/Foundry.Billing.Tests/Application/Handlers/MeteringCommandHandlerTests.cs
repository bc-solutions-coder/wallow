using Foundry.Billing.Application.Metering.Commands.RemoveQuotaOverride;
using Foundry.Billing.Application.Metering.Commands.SetQuotaOverride;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Handlers;

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
    public async Task Handle_WithValidCommand_CreatesQuotaOverride()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideCommand command = new(tenantId, "api.calls", 1000m, QuotaPeriod.Monthly, QuotaAction.Block);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns(meter);

        _quotaRepository.GetTenantOverrideAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Add(Arg.Any<QuotaDefinition>());
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingOverride_UpdatesExistingQuota()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideCommand command = new(tenantId, "api.calls", 5000m, QuotaPeriod.Monthly, QuotaAction.Warn);

        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns(meter);

        QuotaDefinition existingQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            Shared.Kernel.Identity.TenantId.Create(tenantId),
            1000m,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns(existingQuota);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existingQuota.Limit.Should().Be(5000m);
        existingQuota.OnExceeded.Should().Be(QuotaAction.Warn);
        _quotaRepository.DidNotReceive().Add(Arg.Any<QuotaDefinition>());
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentMeter_ReturnsNotFoundFailure()
    {
        SetQuotaOverrideCommand command = new(Guid.NewGuid(), "nonexistent.meter", 100m, QuotaPeriod.Daily, QuotaAction.Block);

        _meterRepository.GetByCodeAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns((MeterDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        _quotaRepository.DidNotReceive().Add(Arg.Any<QuotaDefinition>());
        await _quotaRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CalledTwiceWithSameMeterCode_SecondCallUpdatesExisting()
    {
        Guid tenantId = Guid.NewGuid();
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        _meterRepository.GetByCodeAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(meter);

        SetQuotaOverrideCommand firstCommand = new(tenantId, "api.calls", 1000m, QuotaPeriod.Monthly, QuotaAction.Block);
        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result result1 = await _handler.Handle(firstCommand, CancellationToken.None);

        QuotaDefinition createdQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            Shared.Kernel.Identity.TenantId.Create(tenantId),
            1000m,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync("api.calls", Arg.Any<CancellationToken>())
            .Returns(createdQuota);

        SetQuotaOverrideCommand secondCommand = new(tenantId, "api.calls", 2000m, QuotaPeriod.Monthly, QuotaAction.Warn);
        Result result2 = await _handler.Handle(secondCommand, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Add(Arg.Any<QuotaDefinition>());
        createdQuota.Limit.Should().Be(2000m);
        createdQuota.OnExceeded.Should().Be(QuotaAction.Warn);
    }
}

public class RemoveQuotaOverrideHandlerTests
{
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly RemoveQuotaOverrideHandler _handler;

    public RemoveQuotaOverrideHandlerTests()
    {
        _quotaRepository = Substitute.For<IQuotaDefinitionRepository>();
        _handler = new RemoveQuotaOverrideHandler(_quotaRepository);
    }

    [Fact]
    public async Task Handle_WithExistingOverride_RemovesQuotaOverride()
    {
        Guid tenantId = Guid.NewGuid();
        RemoveQuotaOverrideCommand command = new(tenantId, "api.calls");

        QuotaDefinition existingQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            Shared.Kernel.Identity.TenantId.Create(tenantId),
            1000m,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns(existingQuota);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Remove(existingQuota);
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentOverride_ReturnsNotFoundFailure()
    {
        Guid tenantId = Guid.NewGuid();
        RemoveQuotaOverrideCommand command = new(tenantId, "nonexistent.meter");

        _quotaRepository.GetTenantOverrideAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        _quotaRepository.DidNotReceive().Remove(Arg.Any<QuotaDefinition>());
        await _quotaRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CalledTwiceForSameOverride_SecondCallFails()
    {
        Guid tenantId = Guid.NewGuid();
        RemoveQuotaOverrideCommand command = new(tenantId, "api.calls");

        QuotaDefinition existingQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            Shared.Kernel.Identity.TenantId.Create(tenantId),
            1000m,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync(command.MeterCode, Arg.Any<CancellationToken>())
            .Returns(existingQuota, (QuotaDefinition?)null);

        Result result1 = await _handler.Handle(command, CancellationToken.None);
        Result result2 = await _handler.Handle(command, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
        result2.Error.Code.Should().Contain("NotFound");
        _quotaRepository.Received(1).Remove(Arg.Any<QuotaDefinition>());
    }
}
