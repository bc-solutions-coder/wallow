using Asp.Versioning;
using Foundry.Shared.Api.Extensions;
using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Queries.GetMeterDefinitions;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/metering/meters")]
[Authorize]
[Tags("Metering")]
[Produces("application/json")]
public class MetersController : ControllerBase
{
    private readonly IMessageBus _bus;

    public MetersController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get all meter definitions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MeterDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MeterDefinitionDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(
            new GetMeterDefinitionsQuery(), cancellationToken);

        return result.ToActionResult();
    }
}
