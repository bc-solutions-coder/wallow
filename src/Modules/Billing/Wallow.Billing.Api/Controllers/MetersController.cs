using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Queries.GetMeterDefinitions;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/metering/meters")]
[Authorize]
[Tags("Metering")]
[Produces("application/json")]
public class MetersController(IMessageBus bus) : ControllerBase
{

    /// <summary>
    /// Get all meter definitions.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.BillingRead)]
    [ProducesResponseType(typeof(IReadOnlyList<MeterDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MeterDefinitionDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(
            new GetMeterDefinitionsQuery(), cancellationToken);

        return result.ToActionResult();
    }
}
