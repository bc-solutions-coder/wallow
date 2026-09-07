using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Notifications.Api.Contracts.Push;
using Wallow.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;
using Wallow.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;
using Wallow.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;
using Wallow.Notifications.Application.Channels.Push.DTOs;
using Wallow.Notifications.Application.Channels.Push.Queries.GetTenantPushConfig;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Notifications.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/admin/push/config")]
[Tags("Admin - Push Configuration")]
[Produces("application/json")]
public class PushConfigurationController(IMessageBus bus, ITenantContext tenantContext, Application.Channels.Push.Interfaces.IWebPushConfiguration webPushConfiguration) : ControllerBase
{
    /// <summary>
    /// List tenant Web Push signing key versions.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant. Returns key IDs, public keys, and current or retired
    /// flags without private keys or credentials. Returns an empty list when no valid Web Push configuration
    /// exists.
    /// </remarks>
    [HttpGet("web-push/keys")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(typeof(IReadOnlyList<Application.Channels.Push.Interfaces.WebPushKeyVersion>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebPushKeys(CancellationToken ct) => Ok(await webPushConfiguration.GetVersionsAsync(ct));

    /// <summary>
    /// Create a new current Web Push signing key.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant and a valid VAPID contact subject. Returns the new key ID
    /// and public key, retaining previous nonretired keys for existing subscriptions. Rotation preserves the
    /// configuration's enabled state; private keys are never returned.
    /// </remarks>
    /// <param name="request">VAPID subject as a mailto address or an HTTPS URI.</param>
    /// <param name="ct">Cancels the request.</param>
    [HttpPost("web-push/keys")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(typeof(Application.Channels.Push.Interfaces.WebPushPublicKey), StatusCodes.Status200OK)]
    public async Task<IActionResult> RotateWebPushKey([FromBody] RotateWebPushKeyRequest request, CancellationToken ct)
    {
        Application.Channels.Push.Interfaces.WebPushPublicKey? key = await webPushConfiguration.RotateAsync(request.Subject, ct);
        return key is null ? Result.Failure(Domain.Errors.NotificationsErrors.WebPushInvalidConfiguration).ToActionResult() : Ok(key);
    }

    /// <summary>
    /// Retire a tenant Web Push signing key.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant. Removes the private key and disables delivery for
    /// subscriptions bound to this key. Retiring the current key also leaves no current key for new
    /// subscriptions until another rotation. Returns 404 if the key or a valid configuration cannot be found.
    /// </remarks>
    [HttpDelete("web-push/keys/{keyId}")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetireWebPushKey(string keyId, CancellationToken ct)
    {
        bool retired = await webPushConfiguration.RetireAsync(keyId, ct);
        return retired ? NoContent() : Result.Failure(Domain.Errors.NotificationsErrors.TenantPushConfigurationNotFound).ToActionResult();
    }

    /// <summary>
    /// Get a tenant push configuration.
    /// </summary>
    /// <remarks>
    /// Requires PushRead in the current tenant. Returns one stored platform configuration and its enabled state,
    /// or no content when none exists. The credentials field always contains [redacted]; neither plaintext nor
    /// encrypted credentials are returned. When multiple platforms are configured, the selected platform is
    /// unspecified.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.PushRead)]
    [ProducesResponseType(typeof(TenantPushConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetTenantPushConfig(CancellationToken ct)
    {
        Guid tenantId = tenantContext.TenantId.Value;

        Result<TenantPushConfigDto?> result = await bus.InvokeAsync<Result<TenantPushConfigDto?>>(
            new GetTenantPushConfigQuery(tenantId), ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        if (result.Value is null)
        {
            return NoContent();
        }

        TenantPushConfigDto dto = result.Value;
        TenantPushConfigResponse response = new(dto.Id, dto.TenantId, dto.Platform, dto.EncryptedCredentials, dto.IsEnabled);
        return Ok(response);
    }

    /// <summary>
    /// Set tenant push provider credentials.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant. Creates a configuration for the specified platform or
    /// replaces its credentials while preserving an existing enabled state. Credentials are encrypted for
    /// storage and are not returned. Web Push replacements must preserve signing key history and cannot remove
    /// or reactivate retired keys.
    /// </remarks>
    [HttpPut]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpsertTenantPushConfig(
        [FromBody] UpsertTenantPushConfigRequest request,
        CancellationToken ct)
    {
        TenantId tenantId = tenantContext.TenantId;

        Result result = await bus.InvokeAsync<Result>(
            new UpsertTenantPushConfigCommand(tenantId, request.Platform, request.Credentials), ct);

        return result.ToNoContentResult();
    }

    /// <summary>
    /// Enable or disable a tenant push platform.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant. Changes the specified platform's enabled state while
    /// retaining its credentials and registered devices. Returns no content, or 404 when that platform has no
    /// configuration.
    /// </remarks>
    [HttpPatch("enabled")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTenantPushEnabled(
        [FromBody] SetTenantPushEnabledRequest request,
        CancellationToken ct)
    {
        TenantId tenantId = tenantContext.TenantId;

        Result result = await bus.InvokeAsync<Result>(
            new SetTenantPushEnabledCommand(tenantId, request.Platform, request.IsEnabled), ct);

        return result.ToNoContentResult();
    }

    /// <summary>
    /// Remove a tenant push platform configuration.
    /// </summary>
    /// <remarks>
    /// Requires PushConfigWrite in the current tenant. Deletes the specified platform's credentials and
    /// configuration, returning no content even when no matching configuration exists. Web Push configuration
    /// cannot be removed this way; disable it or retire its signing keys instead.
    /// </remarks>
    [HttpDelete("{platform}")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTenantPushConfig(
        PushPlatform platform,
        CancellationToken ct)
    {
        TenantId tenantId = tenantContext.TenantId;

        Result result = await bus.InvokeAsync<Result>(
            new RemoveTenantPushConfigCommand(tenantId, platform), ct);

        return result.ToNoContentResult();
    }
}

public sealed record SetTenantPushEnabledRequest(
    PushPlatform Platform,
    bool IsEnabled);

public sealed record RotateWebPushKeyRequest(string Subject);
