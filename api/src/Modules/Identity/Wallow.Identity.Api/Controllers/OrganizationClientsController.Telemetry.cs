using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wallow.Identity.Application.DTOs;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

public partial class OrganizationClientsController
{
    /// <summary>Enable observability for an existing organization client.</summary>
    /// <remarks>Requires OrganizationClientsManage. Reveals a separate server ingestion credential once.
    /// Provisioning runs asynchronously; a temporary gateway outage does not fail this operation.</remarks>
    [HttpPost("{clientId}/observability")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(TelemetryEnableResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryEnableResult>> EnableObservability(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        TelemetryEnableResult? result = await clients.EnableTelemetryAsync(orgId, clientId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Rotate an observability credential with a 24-hour overlap after acknowledgement.</summary>
    /// <remarks>Requires OrganizationClientsManage. Reveals the replacement secret once.</remarks>
    [HttpPost("{clientId}/observability/rotate")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(TelemetryEnableResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryEnableResult>> RotateObservability(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct)) { return NotFound(); }
        TelemetryEnableResult? result = await clients.RotateTelemetryAsync(orgId, clientId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Revoke all observability credentials for an organization client.</summary>
    /// <remarks>Requires OrganizationClientsManage. Returns pending revocation until gateway acknowledgement.</remarks>
    [HttpPost("{clientId}/observability/revoke")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(TelemetryStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TelemetryStatusDto>> RevokeObservability(Guid orgId, string clientId, CancellationToken ct) => DisableObservability(orgId, clientId, ct);

    /// <summary>Disable new telemetry collection while preserving stored history.</summary>
    /// <remarks>Requires OrganizationClientsManage. Revokes ingestion when the gateway acknowledges the change.</remarks>
    [HttpPost("{clientId}/observability/disable")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(TelemetryStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryStatusDto>> DisableObservability(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct)) { return NotFound(); }
        TelemetryStatusDto? result = await clients.RevokeTelemetryAsync(orgId, clientId, ct);
        return result is null ? NotFound() : Ok(result);
    }

}
