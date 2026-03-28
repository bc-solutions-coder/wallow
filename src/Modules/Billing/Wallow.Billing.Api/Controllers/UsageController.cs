using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Queries.GetCurrentUsage;
using Wallow.Billing.Application.Metering.Queries.GetUsageHistory;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/metering/usage")]
[Authorize]
[Tags("Metering")]
[Produces("application/json")]
[IgnoreAntiforgeryToken]
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
