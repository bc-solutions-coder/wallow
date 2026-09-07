using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Api.Settings;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity")]
[Authorize]
[Tags("Identity Settings")]
[Produces("application/json")]
public class IdentitySettingsController(
    [FromKeyedServices("identity")] ISettingsService settingsService,
    [FromKeyedServices("identity")] ISettingRegistry settingRegistry,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Get effective Identity configuration for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and resolved tenant context. Returns a key-value map of Identity settings with
    /// user overrides taking precedence over tenant overrides and registered defaults.
    /// </remarks>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ResolvedSettingsConfig), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        ResolvedSettingsConfig config = await settingsService.GetConfigAsync(tenantId, userId.Value, cancellationToken);
        return Result<ResolvedSettingsConfig>.Success(config).ToActionResult();
    }

    /// <summary>
    /// List resolved tenant Identity settings.
    /// </summary>
    /// <remarks>
    /// Requires the SystemSettings permission and resolved tenant context. Returns tenant overrides merged with
    /// registered defaults, including each setting source and available metadata.
    /// </remarks>
    [HttpGet("settings/tenant")]
    [HasPermission(PermissionType.SystemSettings)]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantSettings(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;

        IReadOnlyList<ResolvedSetting> settings = await settingsService.GetTenantSettingsAsync(tenantId, cancellationToken);
        return Result<IReadOnlyList<ResolvedSetting>>.Success(settings).ToActionResult();
    }

    /// <summary>
    /// List resolved Identity settings for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and resolved tenant context. Returns user overrides merged with tenant
    /// overrides and registered defaults, including each value source and available metadata.
    /// </remarks>
    [HttpGet("settings/user")]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserSettings(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        IReadOnlyList<ResolvedSetting> settings = await settingsService.GetUserSettingsAsync(tenantId, userId.Value, cancellationToken);
        return Result<IReadOnlyList<ResolvedSetting>>.Success(settings).ToActionResult();
    }

    /// <summary>
    /// Set an Identity setting override for the tenant.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user, the SystemSettings permission, and resolved tenant context. Creates or
    /// replaces one tenant override using a registered Identity key or a custom. key and a string value. System keys
    /// and unknown keys are rejected; success returns no content.
    /// </remarks>
    [HttpPut("settings/tenant")]
    [HasPermission(PermissionType.SystemSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpsertTenantSetting(
        [FromBody] SettingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        Result validation = ValidateSettingKey(request.Key);
        if (!validation.IsSuccess)
        {
            return validation.ToActionResult();
        }

        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        List<SettingUpdate> updates = [new SettingUpdate(request.Key, request.Value)];
        await settingsService.UpdateTenantSettingsAsync(tenantId, updates, userId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Remove an Identity setting override for the tenant.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user, the SystemSettings permission, and resolved tenant context. Removes the tenant
    /// override so resolution falls back to the registered default. Registered Identity keys and custom. keys are
    /// accepted; system keys and unknown keys are rejected.
    /// </remarks>
    /// <param name="key">Registered Identity setting key or custom. key whose override is removed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpDelete("settings/tenant")]
    [HasPermission(PermissionType.SystemSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTenantSetting(
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        Result validation = ValidateSettingKey(key);
        if (!validation.IsSuccess)
        {
            return validation.ToActionResult();
        }

        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        List<string> keys = [key];
        await settingsService.DeleteTenantSettingsAsync(tenantId, keys, userId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Set an Identity setting override for the user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and resolved tenant context. Creates or replaces one user override using a
    /// registered Identity key or a custom. key and a string value. System keys and unknown keys are rejected;
    /// success returns no content.
    /// </remarks>
    [HttpPut("settings/user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpsertUserSetting(
        [FromBody] SettingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        Result validation = ValidateSettingKey(request.Key);
        if (!validation.IsSuccess)
        {
            return validation.ToActionResult();
        }

        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        List<SettingUpdate> updates = [new SettingUpdate(request.Key, request.Value)];
        await settingsService.UpdateUserSettingsAsync(tenantId, userId.Value, updates, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Remove an Identity setting override for the user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and resolved tenant context. Removes the user override so resolution falls back
    /// to the tenant value or registered default. Registered Identity keys and custom. keys are accepted; system keys
    /// and unknown keys are rejected.
    /// </remarks>
    /// <param name="key">Registered Identity setting key or custom. key whose override is removed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpDelete("settings/user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUserSetting(
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        Result validation = ValidateSettingKey(key);
        if (!validation.IsSuccess)
        {
            return validation.ToActionResult();
        }

        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        List<string> keys = [key];
        await settingsService.DeleteUserSettingsAsync(tenantId, userId.Value, keys, cancellationToken);
        return NoContent();
    }

    private Result ValidateSettingKey(string key)
    {
        return SettingKeyValidator.Validate(key, settingRegistry).ToResult(key);
    }
}
