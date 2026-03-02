using System.Security.Claims;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Announcements.Commands.DismissAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetActiveAnnouncements;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Foundry.Shared.Kernel.Services;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class AnnouncementsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly AnnouncementsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AnnouncementsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new AnnouncementsController(_bus, _tenantContext, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("plan", "Pro")
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetAnnouncements

    [Fact]
    public async Task GetAnnouncements_WithValidUser_ReturnsOkWithAnnouncements()
    {
        AnnouncementDto dto = new(Guid.NewGuid(), "Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, true, true, null, null, null, AnnouncementStatus.Published, DateTime.UtcNow);
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(new List<AnnouncementDto> { dto }));

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().HaveCount(1);
        responses[0].Title.Should().Be("Title");
        responses[0].Type.Should().Be("Feature");
    }

    [Fact]
    public async Task GetAnnouncements_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAnnouncements_PassesUserContextToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(new List<AnnouncementDto>()));

        await _controller.GetAnnouncements(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            Arg.Is<GetActiveAnnouncementsQuery>(q =>
                q.UserId == _userId &&
                q.TenantId == _tenantId &&
                q.PlanName == "Pro" &&
                q.Roles.Contains("Admin")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAnnouncements_WithNoPlanClaim_PassesNullPlanName()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
                }, "TestAuth"))
            }
        };
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(new List<AnnouncementDto>()));

        await _controller.GetAnnouncements(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            Arg.Is<GetActiveAnnouncementsQuery>(q => q.PlanName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAnnouncements_MapsAllFieldsCorrectly()
    {
        DateTime createdAt = DateTime.UtcNow;
        AnnouncementDto dto = new(Guid.NewGuid(), "My Title", "My Content", AnnouncementType.Alert,
            AnnouncementTarget.All, null, null, null, false, true,
            "https://example.com", "Click me", "https://img.example.com/img.png",
            AnnouncementStatus.Published, createdAt);
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(new List<AnnouncementDto> { dto }));

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        AnnouncementResponse response = responses[0];
        response.Title.Should().Be("My Title");
        response.Content.Should().Be("My Content");
        response.Type.Should().Be("Alert");
        response.IsPinned.Should().BeFalse();
        response.IsDismissible.Should().BeTrue();
        response.ActionUrl.Should().Be("https://example.com");
        response.ActionLabel.Should().Be("Click me");
        response.ImageUrl.Should().Be("https://img.example.com/img.png");
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task GetAnnouncements_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<AnnouncementDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region DismissAnnouncement

    [Fact]
    public async Task DismissAnnouncement_WhenSuccess_Returns204NoContent()
    {
        Guid announcementId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DismissAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.DismissAnnouncement(announcementId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DismissAnnouncement_WhenNotFound_Returns404()
    {
        Guid announcementId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DismissAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Announcement", announcementId)));

        IActionResult result = await _controller.DismissAnnouncement(announcementId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DismissAnnouncement_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.DismissAnnouncement(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DismissAnnouncement_PassesCorrectIdsToCommand()
    {
        Guid announcementId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DismissAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.DismissAnnouncement(announcementId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DismissAnnouncementCommand>(c => c.AnnouncementId == announcementId && c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
