using Asp.Versioning;
using Foundry.Shared.Api.Extensions;
using Foundry.Billing.Application.Metering.Commands.RemoveQuotaOverride;
using Foundry.Billing.Application.Metering.Commands.SetQuotaOverride;
using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Queries.GetQuotaStatus;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/metering/quotas")]
[Authorize]
[Tags("Metering")]
[Produces("application/json")]
[Consumes("application/json")]
public class QuotasController : ControllerBase
{
    private readonly IMessageBus _bus;

    public QuotasController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get quota status for current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuotaStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<QuotaStatusDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<QuotaStatusDto>>>(
            new GetQuotaStatusQuery(), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Set a quota override for a tenant (admin only).
    /// </summary>
    [HttpPut("admin/{tenantId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetOverride(
        Guid tenantId,
        [FromBody] SetQuotaOverrideRequest request,
        CancellationToken cancellationToken)
    {
        SetQuotaOverrideCommand command = new SetQuotaOverrideCommand(
            tenantId,
            request.MeterCode,
            request.Limit,
            request.Period,
            request.OnExceeded);

        Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Remove a quota override for a tenant (admin only).
    /// </summary>
    [HttpDelete("admin/{tenantId:guid}/{meterCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveOverride(
        Guid tenantId,
        string meterCode,
        CancellationToken cancellationToken)
    {
        RemoveQuotaOverrideCommand command = new RemoveQuotaOverrideCommand(tenantId, meterCode);

        Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }
}

public sealed record SetQuotaOverrideRequest(
    string MeterCode,
    decimal Limit,
    QuotaPeriod Period,
    QuotaAction OnExceeded);
