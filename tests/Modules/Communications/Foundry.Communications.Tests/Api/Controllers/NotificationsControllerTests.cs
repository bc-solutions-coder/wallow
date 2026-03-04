using System.Security.Claims;
using Foundry.Communications.Api.Contracts.InApp.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Foundry.Communications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Queries.GetUnreadCount;
using Foundry.Communications.Application.Channels.InApp.Queries.GetUserNotifications;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Foundry.Shared.Kernel.Services;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class NotificationsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly NotificationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);
        _controller = new NotificationsController(_bus, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetNotifications

    [Fact]
    public async Task GetNotifications_WithValidUser_ReturnsOkWithPagedResponse()
    {
        NotificationDto dto = new(Guid.NewGuid(), _userId, "TaskAssigned", "Title", "Message", false, null, DateTime.UtcNow, null);
        PagedResult<NotificationDto> pagedResult = new(new List<NotificationDto> { dto }, 1, 1, 20);
        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        IActionResult result = await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PagedNotificationResponse response = ok.Value.Should().BeOfType<PagedNotificationResponse>().Subject;
        response.Items.Should().HaveCount(1);
        response.TotalCount.Should().Be(1);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetNotifications_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_PassesCorrectParametersToQuery()
    {
        PagedResult<NotificationDto> pagedResult = new(new List<NotificationDto>(), 0, 2, 10);
        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        await _controller.GetNotifications(pageNumber: 2, pageSize: 10, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PagedResult<NotificationDto>>>(
            Arg.Is<GetUserNotificationsQuery>(q => q.UserId == _userId && q.PageNumber == 2 && q.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotifications_MapsNotificationDtoToResponse()
    {
        DateTime createdAt = DateTime.UtcNow;
        DateTime readAt = DateTime.UtcNow.AddMinutes(-5);
        NotificationDto dto = new(Guid.NewGuid(), _userId, "TaskCompleted", "Done", "Task completed", true, readAt, createdAt, createdAt);
        PagedResult<NotificationDto> pagedResult = new(new List<NotificationDto> { dto }, 1, 1, 20);
        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        IActionResult result = await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PagedNotificationResponse response = ok.Value.Should().BeOfType<PagedNotificationResponse>().Subject;
        NotificationResponse notification = response.Items[0];
        notification.UserId.Should().Be(_userId);
        notification.Type.Should().Be("TaskCompleted");
        notification.Title.Should().Be("Done");
        notification.Message.Should().Be("Task completed");
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(readAt);
    }

    [Fact]
    public async Task GetNotifications_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PagedResult<NotificationDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_WithNonGuidUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
                }, "TestAuth"))
            }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        PagedResult<NotificationDto> pagedResult = new(new List<NotificationDto>(), 0, 1, 20);
        _bus.InvokeAsync<Result<PagedResult<NotificationDto>>>(Arg.Any<GetUserNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        await _controller.GetNotifications(cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PagedResult<NotificationDto>>>(
            Arg.Is<GetUserNotificationsQuery>(q => q.UserId == subUserId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetUnreadCount

    [Fact]
    public async Task GetUnreadCount_WithValidUser_ReturnsOkWithCount()
    {
        _bus.InvokeAsync<Result<int>>(Arg.Any<GetUnreadCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(5));

        IActionResult result = await _controller.GetUnreadCount(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        UnreadCountResponse response = ok.Value.Should().BeOfType<UnreadCountResponse>().Subject;
        response.Count.Should().Be(5);
    }

    [Fact]
    public async Task GetUnreadCount_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.GetUnreadCount(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_PassesUserIdToQuery()
    {
        _bus.InvokeAsync<Result<int>>(Arg.Any<GetUnreadCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(0));

        await _controller.GetUnreadCount(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<int>>(
            Arg.Is<GetUnreadCountQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region MarkAsRead

    [Fact]
    public async Task MarkAsRead_WhenSuccess_Returns204NoContent()
    {
        Guid notificationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.MarkAsRead(notificationId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_Returns404()
    {
        Guid notificationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Notification", notificationId)));

        IActionResult result = await _controller.MarkAsRead(notificationId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task MarkAsRead_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task MarkAsRead_PassesCorrectIdsToCommand()
    {
        Guid notificationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.MarkAsRead(notificationId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<MarkNotificationReadCommand>(c => c.NotificationId == notificationId && c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region MarkAllAsRead

    [Fact]
    public async Task MarkAllAsRead_WhenSuccess_Returns204NoContent()
    {
        _bus.InvokeAsync<Result>(Arg.Any<MarkAllNotificationsReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.MarkAllAsRead(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.MarkAllAsRead(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task MarkAllAsRead_PassesUserIdToCommand()
    {
        _bus.InvokeAsync<Result>(Arg.Any<MarkAllNotificationsReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.MarkAllAsRead(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<MarkAllNotificationsReadCommand>(c => c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAllAsRead_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result>(Arg.Any<MarkAllNotificationsReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("SomeError", "Something went wrong")));

        IActionResult result = await _controller.MarkAllAsRead(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion
}
