using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Application.Metering.Queries.GetMeterDefinitions;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Metering;

public class GetMeterDefinitionsHandlerTests
{
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly GetMeterDefinitionsHandler _handler;

    public GetMeterDefinitionsHandlerTests()
    {
        _meterRepository = Substitute.For<IMeterDefinitionRepository>();
        _handler = new GetMeterDefinitionsHandler(_meterRepository);
    }

    [Fact]
    public async Task Handle_WhenMetersExist_ReturnsAllMeterDefinitions()
    {
        MeterDefinition meter1 = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        MeterDefinition meter2 = MeterDefinition.Create("storage.bytes", "Storage", "bytes", MeterAggregation.Max, false);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter1, meter2 });

        Result<IReadOnlyList<MeterDefinitionDto>> result = await _handler.Handle(
            new GetMeterDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Code.Should().Be("api.calls");
        result.Value[0].DisplayName.Should().Be("API Calls");
        result.Value[0].Unit.Should().Be("requests");
        result.Value[0].Aggregation.Should().Be("Sum");
        result.Value[0].IsBillable.Should().BeTrue();
        result.Value[1].Code.Should().Be("storage.bytes");
        result.Value[1].IsBillable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoMeters_ReturnsEmptyList()
    {
        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        Result<IReadOnlyList<MeterDefinitionDto>> result = await _handler.Handle(
            new GetMeterDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsMeterIdsCorrectly()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        _meterRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { meter });

        Result<IReadOnlyList<MeterDefinitionDto>> result = await _handler.Handle(
            new GetMeterDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Id.Should().Be(meter.Id.Value);
    }
}
