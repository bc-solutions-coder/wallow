using Asp.Versioning;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Mappings;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Api.Controllers;

/// <summary>
/// SSO (Single Sign-On) management endpoints for tenant administrators.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/sso")]
[Authorize]
[Tags("SSO")]
[Produces("application/json")]
[Consumes("application/json")]
public class SsoController : ControllerBase
{
    private readonly ISsoService _ssoService;

    public SsoController(ISsoService ssoService)
    {
        _ssoService = ssoService;
    }

    /// <summary>
    /// Get the current SSO configuration for the tenant.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.SsoRead)]
    [ProducesResponseType(typeof(SsoConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoConfigurationDto>> GetConfiguration(CancellationToken ct)
    {
        SsoConfigurationDto? config = await _ssoService.GetConfigurationAsync(ct);
        return config is null ? NotFound() : Ok(config);
    }

    /// <summary>
    /// Configure SAML SSO for the tenant.
    /// </summary>
    [HttpPost("saml")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SsoConfigurationDto>> ConfigureSaml(
        [FromBody] ConfigureSamlSsoRequest request,
        CancellationToken ct)
    {
        SaveSamlConfigRequest saveRequest = new(
            request.DisplayName,
            request.EntityId,
            request.SsoUrl,
            request.SloUrl,
            request.Certificate,
            request.NameIdFormat.ToDomain(),
            request.EmailAttribute,
            request.FirstNameAttribute,
            request.LastNameAttribute,
            request.GroupsAttribute,
            request.EnforceForAllUsers,
            request.AutoProvisionUsers,
            request.DefaultRole,
            request.SyncGroupsAsRoles);

        SsoConfigurationDto config = await _ssoService.SaveSamlConfigurationAsync(saveRequest, ct);
        return Ok(config);
    }

    /// <summary>
    /// Configure OIDC SSO for the tenant.
    /// </summary>
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

        SsoConfigurationDto config = await _ssoService.SaveOidcConfigurationAsync(saveRequest, ct);
        return Ok(config);
    }

    /// <summary>
    /// Test the SSO connection to verify configuration is correct.
    /// </summary>
    [HttpPost("test")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoTestResult>> TestConnection(CancellationToken ct)
    {
        SsoTestResult result = await _ssoService.TestConnectionAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Activate SSO for the tenant (enables the identity provider).
    /// </summary>
    [HttpPost("activate")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate(CancellationToken ct)
    {
        await _ssoService.ActivateAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Disable SSO for the tenant.
    /// </summary>
    [HttpPost("disable")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(CancellationToken ct)
    {
        await _ssoService.DisableAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Get SAML Service Provider metadata XML.
    /// Enterprise IdPs use this to configure their side of the SSO integration.
    /// </summary>
    [HttpGet("saml/metadata")]
    [HasPermission(PermissionType.SsoRead)]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/xml")]
    public async Task<IActionResult> GetSamlMetadata(CancellationToken ct)
    {
        string metadata = await _ssoService.GetSamlServiceProviderMetadataAsync(ct);
        return Content(metadata, "application/xml");
    }

    /// <summary>
    /// Get OIDC callback information for IdP configuration.
    /// Returns redirect URIs needed to configure the enterprise IdP.
    /// </summary>
    [HttpGet("oidc/callback-info")]
    [HasPermission(PermissionType.SsoRead)]
    [ProducesResponseType(typeof(OidcCallbackInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<OidcCallbackInfo>> GetOidcCallbackInfo(CancellationToken ct)
    {
        OidcCallbackInfo callbackInfo = await _ssoService.GetOidcCallbackInfoAsync(ct);
        return Ok(callbackInfo);
    }

    /// <summary>
    /// Validate the IdP configuration (check discovery endpoints, certificates, etc.).
    /// </summary>
    [HttpPost("validate")]
    [HasPermission(PermissionType.SsoManage)]
    [ProducesResponseType(typeof(SsoValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SsoValidationResult>> ValidateConfiguration(CancellationToken ct)
    {
        SsoValidationResult result = await _ssoService.ValidateIdpConfigurationAsync(ct);
        return Ok(result);
    }
}
