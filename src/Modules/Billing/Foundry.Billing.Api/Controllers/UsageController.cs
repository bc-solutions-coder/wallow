using Asp.Versioning;
using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Queries.GetCurrentUsage;
using Foundry.Billing.Application.Metering.Queries.GetUsageHistory;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/metering/usage")]
[Authorize]
[Tags("Metering")]
[Produces("application/json")]
public class UsageController(IMessageBus bus) : ControllerBase
{

    /// <summary>
    /// Get current usage for all meters.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.BillingRead)]
    [ProducesResponseType(typeof(IReadOnlyList<UsageSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] QuotaPeriod? period,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UsageSummaryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(
            new GetCurrentUsageQuery(null, period), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Get current usage for a specific meter.
    /// </summary>
    [HttpGet("{meterCode}")]
    [HasPermission(PermissionType.BillingRead)]
    [ProducesResponseType(typeof(IReadOnlyList<UsageSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByMeterCode(
        string meterCode,
        [FromQuery] QuotaPeriod? period,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UsageSummaryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(
            new GetCurrentUsageQuery(meterCode, period), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Get historical usage for a specific meter.
    /// </summary>
    [HttpGet("{meterCode}/history")]
    [HasPermission(PermissionType.BillingRead)]
    [ProducesResponseType(typeof(IReadOnlyList<UsageRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        string meterCode,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UsageRecordDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(
            new GetUsageHistoryQuery(meterCode, from, to), cancellationToken);

        return result.ToActionResult();
    }
}
