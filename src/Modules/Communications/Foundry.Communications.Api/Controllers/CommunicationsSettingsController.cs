using Asp.Versioning;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/communications")]
[Authorize]
[Tags("Communications Settings")]
[Produces("application/json")]
public class CommunicationsSettingsController(
    ISettingsService settingsService,
    ISettingRegistry settingRegistry,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("config")]
    [ProducesResponseType(typeof(ResolvedSettingsConfig), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        ResolvedSettingsConfig config = await settingsService.GetConfigAsync(tenantId, userId.Value, cancellationToken);
        return Result<ResolvedSettingsConfig>.Success(config).ToActionResult();
    }

    [HttpGet("settings/tenant")]
    [HasPermission(PermissionType.AnnouncementManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantSettings(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;

        IReadOnlyList<ResolvedSetting> settings = await settingsService.GetTenantSettingsAsync(tenantId, cancellationToken);
        return Result<IReadOnlyList<ResolvedSetting>>.Success(settings).ToActionResult();
    }

    [HttpGet("settings/user")]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserSettings(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        IReadOnlyList<ResolvedSetting> settings = await settingsService.GetUserSettingsAsync(tenantId, userId.Value, cancellationToken);
        return Result<IReadOnlyList<ResolvedSetting>>.Success(settings).ToActionResult();
    }

    [HttpPut("settings/tenant")]
    [HasPermission(PermissionType.AnnouncementManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        List<SettingUpdate> updates = [new SettingUpdate(request.Key, request.Value)];
        await settingsService.UpdateTenantSettingsAsync(tenantId, updates, userId.Value, cancellationToken);
        return NoContent();
    }

    [HttpDelete("settings/tenant")]
    [HasPermission(PermissionType.AnnouncementManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        List<string> keys = [key];
        await settingsService.DeleteTenantSettingsAsync(tenantId, keys, userId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPut("settings/user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        List<SettingUpdate> updates = [new SettingUpdate(request.Key, request.Value)];
        await settingsService.UpdateUserSettingsAsync(tenantId, userId.Value, updates, cancellationToken);
        return NoContent();
    }

    [HttpDelete("settings/user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        List<string> keys = [key];
        await settingsService.DeleteUserSettingsAsync(tenantId, userId.Value, keys, cancellationToken);
        return NoContent();
    }

    private Result ValidateSettingKey(string key)
    {
        SettingKeyValidationResult validation = SettingKeyValidator.Validate(key, settingRegistry);

        return validation switch
        {
            SettingKeyValidationResult.System => Result.Failure(
                Error.Validation("Settings.SystemKeyBlocked", "System keys cannot be modified through this endpoint")),
            SettingKeyValidationResult.Unknown => Result.Failure(
                Error.Validation("Settings.UnknownKey", $"Unknown setting key '{key}'")),
            _ => Result.Success()
        };
    }
}

public sealed record SettingUpdateRequest(string Key, string Value);
