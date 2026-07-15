using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Api.Controllers;
using Wallow.Announcements.Application.Announcements.Commands.DismissAnnouncement;
using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Announcements.Tests.Api.Controllers;

public class AnnouncementsControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly AnnouncementsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AnnouncementsControllerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new AnnouncementsController(_bus, _tenantContext, _currentUserService);

        ClaimsPrincipal user = new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        ], "TestAuth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private static AnnouncementDto CreateAnnouncementDto(string title = "Test Announcement")
    {
        return new AnnouncementDto(
            Guid.NewGuid(), title, "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true,
            null, null, null, AnnouncementStatus.Published, DateTime.UtcNow);
    }

    #region GetAnnouncements

    [Fact]
    public async Task GetAnnouncements_WhenSuccess_ReturnsOkWithResponses()
    {
        List<AnnouncementDto> dtos = new()
        {
            CreateAnnouncementDto("Announcement 1"),
            CreateAnnouncementDto("Announcement 2")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(dtos));

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAnnouncements_WhenUserNotAuthenticated_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetAnnouncements_PassesCorrectUserIdToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>([]));

        await _controller.GetAnnouncements(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            Arg.Is<GetActiveAnnouncementsQuery>(q =>
                q.UserId == _userId &&
                q.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAnnouncements_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetActiveAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>([]));

        IActionResult result = await _controller.GetAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().BeEmpty();
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
    public async Task DismissAnnouncement_WhenUserNotAuthenticated_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.DismissAnnouncement(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
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
    public async Task DismissAnnouncement_PassesCorrectFieldsToCommand()
    {
        Guid announcementId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DismissAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.DismissAnnouncement(announcementId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DismissAnnouncementCommand>(c =>
                c.AnnouncementId == announcementId &&
                c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DismissAnnouncement_WhenValidationFailure_Returns400()
    {
        Guid announcementId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DismissAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Cannot dismiss this announcement")));

        IActionResult result = await _controller.DismissAnnouncement(announcementId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion
}
