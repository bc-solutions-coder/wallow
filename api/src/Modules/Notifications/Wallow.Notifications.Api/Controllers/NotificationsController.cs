using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Notifications.Api.Contracts.InApp.Responses;
using Wallow.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Wallow.Notifications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Application.Channels.InApp.Queries.GetUnreadCount;
using Wallow.Notifications.Application.Channels.InApp.Queries.GetUserNotifications;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Notifications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/notifications")]
[Authorize]
[Tags("Notifications")]
[Produces("application/json")]
[Consumes("application/json")]
public class NotificationsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// List the current user's notification history.
    /// </summary>
    /// <remarks>
    /// Requires NotificationRead and an authenticated user in the current tenant. Returns a page ordered newest
    /// first, excluding archived and expired notifications. Pagination metadata includes the total matching
    /// count and whether adjacent pages exist.
    /// </remarks>
    /// <param name="pageNumber">One-based page number; defaults to 1.</param>
    /// <param name="pageSize">Number of notifications per page; defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(typeof(PagedNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result<PagedResult<NotificationDto>> result = await bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(
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
    /// Count the current user's unread notifications.
    /// </summary>
    /// <remarks>
    /// Requires NotificationRead and an authenticated user in the current tenant. Counts all unread
    /// notifications for that user, including archived or expired notifications that the history endpoint omits.
    /// Returns a count without changing read state.
    /// </remarks>
    [HttpGet("unread-count")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result<int> result = await bus.InvokeAsync<Result<int>>(
            new GetUnreadCountQuery(userId.Value), cancellationToken);

        return result.Map(count => new UnreadCountResponse(count)).ToActionResult();
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    /// <remarks>
    /// Requires NotificationRead and ownership of the notification in the current tenant. Records the current
    /// read time and returns no content, updating the timestamp even for an already read notification. Returns
    /// 404 for an unknown notification and an access-denied error for another user's notification.
    /// </remarks>
    [HttpPost("{id:guid}/read")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
            new MarkNotificationReadCommand(id, userId.Value), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Mark all of the current user's notifications as read.
    /// </summary>
    /// <remarks>
    /// Requires NotificationRead and an authenticated user in the current tenant. Marks every unread
    /// notification for that user as read, including archived or expired notifications and notifications outside
    /// the current history page. Returns no content, including when there are no unread notifications.
    /// </remarks>
    [HttpPost("read-all")]
    [HasPermission(PermissionType.NotificationRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
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
        dto.ActionUrl,
        dto.CreatedAt,
        dto.UpdatedAt);
}
