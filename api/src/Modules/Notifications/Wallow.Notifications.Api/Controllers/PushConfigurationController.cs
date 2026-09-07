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
    [HttpGet("web-push/keys")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(typeof(IReadOnlyList<Application.Channels.Push.Interfaces.WebPushKeyVersion>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebPushKeys(CancellationToken ct) => Ok(await webPushConfiguration.GetVersionsAsync(ct));

    [HttpPost("web-push/keys")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(typeof(Application.Channels.Push.Interfaces.WebPushPublicKey), StatusCodes.Status200OK)]
    public async Task<IActionResult> RotateWebPushKey([FromBody] RotateWebPushKeyRequest request, CancellationToken ct)
    {
        Application.Channels.Push.Interfaces.WebPushPublicKey? key = await webPushConfiguration.RotateAsync(request.Subject, ct);
        return key is null ? Result.Failure(Domain.Errors.NotificationsErrors.WebPushInvalidConfiguration).ToActionResult() : Ok(key);
    }

    [HttpDelete("web-push/keys/{keyId}")]
    [HasPermission(PermissionType.PushConfigWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetireWebPushKey(string keyId, CancellationToken ct)
    {
        bool retired = await webPushConfiguration.RetireAsync(keyId, ct);
        return retired ? NoContent() : Result.Failure(Domain.Errors.NotificationsErrors.TenantPushConfigurationNotFound).ToActionResult();
    }

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
