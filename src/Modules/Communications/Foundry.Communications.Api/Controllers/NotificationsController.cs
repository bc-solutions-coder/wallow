using Asp.Versioning;
using Foundry.Communications.Api.Contracts.InApp.Responses;
using Foundry.Communications.Api.Extensions;
using Foundry.Communications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Foundry.Communications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Queries.GetUnreadCount;
using Foundry.Communications.Application.Channels.InApp.Queries.GetUserNotifications;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
[Tags("Notifications")]
[Produces("application/json")]
[Consumes("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(IMessageBus bus, ICurrentUserService currentUserService)
    {
        _bus = bus;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get the current user's notification history.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(typeof(PagedNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result<PagedResult<NotificationDto>> result = await _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(
            new GetUserNotificationsQuery(userId.Value, pageNumber, pageSize), cancellationToken);

        return result.Map(paged => new PagedNotificationResponse(
            paged.Items.Select(ToResponse).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize,
            paged.TotalPages,
            paged.HasPreviousPage,
            paged.HasNextPage))
            .ToActionResult();
    }

    /// <summary>
    /// Get the current user's unread notification count.
    /// </summary>
    [HttpGet("unread-count")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result<int> result = await _bus.InvokeAsync<Result<int>>(
            new GetUnreadCountQuery(userId.Value), cancellationToken);

        return result.Map(count => new UnreadCountResponse(count)).ToActionResult();
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result result = await _bus.InvokeAsync<Result>(
            new MarkNotificationReadCommand(id, userId.Value), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read for the current user.
    /// </summary>
    [HttpPost("read-all")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result result = await _bus.InvokeAsync<Result>(
            new MarkAllNotificationsReadCommand(userId.Value), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    private static NotificationResponse ToResponse(NotificationDto dto) => new(
        dto.Id,
        dto.UserId,
        dto.Type,
        dto.Title,
        dto.Message,
        dto.IsRead,
        dto.ReadAt,
        dto.CreatedAt,
        dto.UpdatedAt);
}
