using Asp.Versioning;
using Foundry.Notifications.Api.Contracts.Preferences;
using Foundry.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;
using Foundry.Notifications.Application.Channels.Preferences.DTOs;
using Foundry.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;
using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Notifications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/notification-settings")]
[Authorize]
[Tags("Notification Settings")]
[Produces("application/json")]
[Consumes("application/json")]
public class UserNotificationSettingsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(typeof(UserNotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserNotificationSettings(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result<UserNotificationSettingsDto> result = await bus.InvokeAsync<Result<UserNotificationSettingsDto>>(
            new GetUserNotificationSettingsQuery(userId.Value), cancellationToken);

        return result.Map(ToResponse).ToActionResult();
    }

    [HttpPut("channel")]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetChannelEnabled(
        [FromBody] SetChannelEnabledRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        SetChannelEnabledCommand command = new(userId.Value, request.ChannelType, request.IsEnabled);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    [HttpPut("type")]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetNotificationTypeEnabled(
        [FromBody] SetNotificationTypeEnabledRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        SetChannelEnabledCommand command = new(
            userId.Value,
            request.ChannelType,
            request.IsEnabled,
            request.NotificationType);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    private static UserNotificationSettingsResponse ToResponse(UserNotificationSettingsDto dto) =>
        new(dto.UserId, dto.ChannelSettings.Select(ToChannelResponse).ToList());

    private static ChannelSettingResponse ToChannelResponse(ChannelSettingDto dto) =>
        new(dto.ChannelType, dto.IsGloballyEnabled, dto.TypePreferences.Select(ToPrefResponse).ToList());

    private static ChannelPreferenceResponse ToPrefResponse(ChannelPreferenceDto dto) =>
        new(dto.Id, dto.ChannelType, dto.NotificationType, dto.IsEnabled, dto.CreatedAt, dto.UpdatedAt);
}
