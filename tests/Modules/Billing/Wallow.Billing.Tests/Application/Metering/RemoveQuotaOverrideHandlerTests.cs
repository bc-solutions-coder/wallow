using Wallow.Billing.Application.Metering.Commands.RemoveQuotaOverride;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Metering;

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
    public async Task Handle_WhenOverrideExists_RemovesQuota()
    {
        Guid tenantId = Guid.NewGuid();
        RemoveQuotaOverrideCommand command = new(tenantId, "api.calls");

        QuotaDefinition existingQuota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            TenantId.Create(tenantId),
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns(existingQuota);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _quotaRepository.Received(1).Remove(existingQuota);
        await _quotaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOverrideDoesNotExist_ReturnsNotFoundError()
    {
        RemoveQuotaOverrideCommand command = new(Guid.NewGuid(), "api.calls");

        _quotaRepository.GetTenantOverrideAsync(
                "api.calls",
                Arg.Any<CancellationToken>())
            .Returns((QuotaDefinition?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        _quotaRepository.DidNotReceive().Remove(Arg.Any<QuotaDefinition>());
    }
}
