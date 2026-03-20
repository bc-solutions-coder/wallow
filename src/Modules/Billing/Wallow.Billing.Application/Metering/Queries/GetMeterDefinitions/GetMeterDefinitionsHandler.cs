using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Metering.Queries.GetMeterDefinitions;

public sealed class GetMeterDefinitionsHandler(IMeterDefinitionRepository meterRepository)
{
    public async Task<Result<IReadOnlyList<MeterDefinitionDto>>> Handle(
        GetMeterDefinitionsQuery _,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MeterDefinition> meters = await meterRepository.GetAllAsync(cancellationToken);

        List<MeterDefinitionDto> dtos = meters.Select(m => new MeterDefinitionDto(
            Id: m.Id.Value,
            Code: m.Code,
            DisplayName: m.DisplayName,
            Unit: m.Unit,
            Aggregation: m.Aggregation.ToString(),
            IsBillable: m.IsBillable)).ToList();

        return dtos;
    }
}
