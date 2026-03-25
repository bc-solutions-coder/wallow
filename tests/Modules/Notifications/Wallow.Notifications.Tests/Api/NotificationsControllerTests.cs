using Wallow.Notifications.Api.Controllers;
using Wallow.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Wallow.Notifications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Application.Channels.InApp.Queries.GetUnreadCount;
using Wallow.Notifications.Application.Channels.InApp.Queries.GetUserNotifications;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Notifications.Tests.Api;

public class NotificationsControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(_bus, _currentUserService);
    }

    [Fact]
    public async Task GetNotifications_WhenUserFound_ReturnsOkWithPagedResult()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        NotificationDto dto = new(
            Guid.NewGuid(), userId, NotificationType.TaskAssigned.ToString(),
            "Title", "Body", false, null, null, DateTime.UtcNow, null);

        PagedResult<NotificationDto> pagedResult = new(new List<NotificationDto> { dto }, 1, 1, 20);
        Result<PagedResult<NotificationDto>> result = Result.Success(pagedResult);

        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(
            Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        IActionResult response = await _controller.GetNotifications(1, 20, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetNotifications_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.GetNotifications(1, 20, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetUnreadCount_WhenUserFound_ReturnsCount()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        _bus.InvokeAsync<Result<int>>(Arg.Any<GetUnreadCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(5));

        IActionResult response = await _controller.GetUnreadCount(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUnreadCount_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.GetUnreadCount(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task MarkAsRead_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task MarkAsRead_WhenSuccess_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult response = await _controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_WhenSuccess_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult response = await _controller.MarkAllAsRead(CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.MarkAllAsRead(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task MarkAsRead_WhenFailure_ReturnsErrorResult()
    {
        Guid userId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = Error.NotFound("Notification", notificationId);
        _bus.InvokeAsync<Result>(
            Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        IActionResult response = await _controller.MarkAsRead(notificationId, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenFailure_ReturnsErrorResult()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = Error.Validation("No notifications to mark as read");
        _bus.InvokeAsync<Result>(
            Arg.Any<MarkAllNotificationsReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        IActionResult response = await _controller.MarkAllAsRead(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetNotifications_WhenFailure_ReturnsErrorResult()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = Error.Validation("Invalid page number");
        Result<PagedResult<NotificationDto>> failureResult = Result.Failure<PagedResult<NotificationDto>>(error);

        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(
            Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(failureResult);

        IActionResult response = await _controller.GetNotifications(0, 20, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetUnreadCount_WhenFailure_ReturnsErrorResult()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = Error.Validation("Unable to retrieve count");
        Result<int> failureResult = Result.Failure<int>(error);

        _bus.InvokeAsync<Result<int>>(
            Arg.Any<GetUnreadCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(failureResult);

        IActionResult response = await _controller.GetUnreadCount(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }
}
