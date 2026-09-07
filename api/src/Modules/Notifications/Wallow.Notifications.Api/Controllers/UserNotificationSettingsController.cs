using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Notifications.Api.Contracts.Preferences;
using Wallow.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;
using Wallow.Notifications.Application.Channels.Preferences.DTOs;
using Wallow.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;
using Wallow.Notifications.Application.Preferences.DTOs;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Notifications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/notification-settings")]
[Authorize]
[Tags("Notification Settings")]
[Produces("application/json")]
[Consumes("application/json")]
public class UserNotificationSettingsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Get the current user's notification preferences.
    /// </summary>
    /// <remarks>
    /// Requires EmailPreferenceManage and an authenticated user in the current tenant. Returns stored
    /// preferences grouped by channel, with each channel's overall enabled state and per-type overrides.
    /// Channels without stored preferences are omitted; delivery defaults to enabled when no preference disables
    /// it.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(typeof(UserNotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserNotificationSettings(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result<UserNotificationSettingsDto> result = await bus.InvokeAsync<Result<UserNotificationSettingsDto>>(
            new GetUserNotificationSettingsQuery(userId.Value), cancellationToken);

        return result.Map(ToResponse).ToActionResult();
    }

    /// <summary>
    /// Enable or disable a notification channel.
    /// </summary>
    /// <remarks>
    /// Requires EmailPreferenceManage and applies to the authenticated user in the current tenant. Creates or
    /// replaces the channel-wide preference without removing per-type preferences. Disabling the channel
    /// overrides all of its per-type settings; enabling it leaves individually disabled types disabled.
    /// </remarks>
    [HttpPut("channel")]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetChannelEnabled(
        [FromBody] SetChannelEnabledRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        SetChannelEnabledCommand command = new(userId.Value, request.ChannelType, request.IsEnabled);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Enable or disable a notification type on a channel.
    /// </summary>
    /// <remarks>
    /// Requires EmailPreferenceManage and applies to the authenticated user in the current tenant. Creates or
    /// replaces the preference for the specified channel and notification type. Enabling a type does not
    /// override a disabled channel; the notification type * addresses the channel-wide preference.
    /// </remarks>
    [HttpPut("type")]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetNotificationTypeEnabled(
        [FromBody] SetNotificationTypeEnabledRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
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
