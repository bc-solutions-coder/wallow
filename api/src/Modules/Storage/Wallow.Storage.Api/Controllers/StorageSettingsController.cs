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

namespace Wallow.Storage.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/storage")]
[Authorize]
[Tags("Storage Settings")]
[Produces("application/json")]
public class StorageSettingsController(
    [FromKeyedServices("storage")] ISettingsService settingsService,
    [FromKeyedServices("storage")] ISettingRegistry settingRegistry,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Get resolved storage configuration.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Returns a key-value map using user overrides first,
    /// then tenant overrides, then registered defaults. Upload enforcement ignores user overrides and uses
    /// tenant limits, so this map can differ from the limits enforced for uploads.
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
    /// Get resolved tenant storage settings.
    /// </summary>
    /// <remarks>
    /// Requires StorageWrite in the current tenant. Returns tenant overrides merged with registered defaults,
    /// including each value's source and descriptive metadata. User overrides are excluded.
    /// </remarks>
    [HttpGet("settings/tenant")]
    [HasPermission(PermissionType.StorageWrite)]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantSettings(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;

        IReadOnlyList<ResolvedSetting> settings = await settingsService.GetTenantSettingsAsync(tenantId, cancellationToken);
        return Result<IReadOnlyList<ResolvedSetting>>.Success(settings).ToActionResult();
    }

    /// <summary>
    /// Get resolved storage settings for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Returns user overrides merged with tenant overrides
    /// and registered defaults, including each value's source and descriptive metadata. User overrides do not
    /// change the tenant limits enforced during upload.
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
    /// Set a tenant storage setting.
    /// </summary>
    /// <remarks>
    /// Requires StorageWrite and an authenticated user in the current tenant. Creates or replaces one
    /// string-valued tenant override and returns no content. Accepts registered storage keys and custom. keys;
    /// system. keys and unknown keys are rejected.
    /// </remarks>
    [HttpPut("settings/tenant")]
    [HasPermission(PermissionType.StorageWrite)]
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
    /// Remove a tenant storage setting override.
    /// </summary>
    /// <remarks>
    /// Requires StorageWrite and an authenticated user in the current tenant. Removes the named override so the
    /// registered default applies where one exists; user overrides remain in place. Returns no content even when
    /// no override exists, but rejects system. keys and unknown keys.
    /// </remarks>
    /// <param name="key">Registered storage key or a key beginning with custom.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpDelete("settings/tenant")]
    [HasPermission(PermissionType.StorageWrite)]
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
    /// Set a storage setting for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Creates or replaces one string-valued user override
    /// used by resolved configuration responses. Accepts registered storage keys and custom. keys; system. keys
    /// and unknown keys are rejected. This override does not raise or otherwise change enforced tenant upload
    /// limits.
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
    /// Remove a user storage setting override.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Removes the named user override so the tenant value
    /// or registered default applies where one exists. Returns no content even when no override exists, but
    /// rejects system. keys and unknown keys.
    /// </remarks>
    /// <param name="key">Registered storage key or a key beginning with custom.</param>
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
