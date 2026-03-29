using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/sso")]
[Authorize]
[Tags("SSO")]
[Produces("application/json")]
[Consumes("application/json")]
[IgnoreAntiforgeryToken]
public class SsoController(ISsoService ssoService) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionType.SsoRead)]
    [ProducesResponseType(typeof(SsoConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoConfigurationDto>> GetConfiguration(CancellationToken ct)
    {
        SsoConfigurationDto? config = await ssoService.GetConfigurationAsync(ct);
        return config is null ? NotFound() : Ok(config);
    }

    [HttpPost("oidc")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SsoConfigurationDto>> ConfigureOidc(
        [FromBody] ConfigureOidcSsoRequest request,
        CancellationToken ct)
    {
        SaveOidcConfigRequest saveRequest = new(
            request.DisplayName,
            request.Issuer,
            request.ClientId,
            request.ClientSecret,
            request.Scopes,
            request.EmailAttribute,
            request.FirstNameAttribute,
            request.LastNameAttribute,
            request.GroupsAttribute,
            request.EnforceForAllUsers,
            request.AutoProvisionUsers,
            request.DefaultRole,
            request.SyncGroupsAsRoles);

        SsoConfigurationDto config = await ssoService.SaveOidcConfigurationAsync(saveRequest, ct);
        return Ok(config);
    }

    [HttpPost("test")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoTestResult>> TestConnection(CancellationToken ct)
    {
        SsoTestResult result = await ssoService.TestConnectionAsync(ct);
        return Ok(result);
    }

    [HttpPost("activate")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate(CancellationToken ct)
    {
        await ssoService.ActivateAsync(ct);
        return NoContent();
    }

    [HttpPost("disable")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(CancellationToken ct)
    {
        await ssoService.DisableAsync(ct);
        return NoContent();
    }

    [HttpGet("oidc/callback-info")]
    [HasPermission(PermissionType.SsoRead)]
    [ProducesResponseType(typeof(OidcCallbackInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<OidcCallbackInfo>> GetOidcCallbackInfo(CancellationToken ct)
    {
        OidcCallbackInfo callbackInfo = await ssoService.GetOidcCallbackInfoAsync(ct);
        return Ok(callbackInfo);
    }

    [HttpPost("validate")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoValidationResult>> ValidateConfiguration(CancellationToken ct)
    {
        SsoValidationResult result = await ssoService.ValidateIdpConfigurationAsync(ct);
        return Ok(result);
    }
}
