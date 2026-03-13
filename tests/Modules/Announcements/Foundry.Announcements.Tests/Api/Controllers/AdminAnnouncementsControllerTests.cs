using Foundry.Announcements.Api.Contracts.Responses;
using Foundry.Announcements.Api.Controllers;
using Foundry.Announcements.Application.Announcements.Commands.ArchiveAnnouncement;
using Foundry.Announcements.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Announcements.Application.Announcements.Commands.PublishAnnouncement;
using Foundry.Announcements.Application.Announcements.Commands.UpdateAnnouncement;
using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Queries.GetAllAnnouncements;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Shared.Infrastructure.Core.Services;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Announcements.Tests.Api.Controllers;

public class AdminAnnouncementsControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IHtmlSanitizationService _sanitizer = Substitute.For<IHtmlSanitizationService>();
    private readonly AdminAnnouncementsController _controller;

    public AdminAnnouncementsControllerTests()
    {
        _sanitizer.Sanitize(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        _controller = new AdminAnnouncementsController(_bus, _sanitizer);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static AnnouncementDto CreateAnnouncementDto(Guid? id = null, string title = "Test Announcement")
    {
        return new AnnouncementDto(
            id ?? Guid.NewGuid(),
            title,
            "Test Content",
            AnnouncementType.Feature,
            AnnouncementTarget.All,
            null,
            null,
            null,
            false,
            true,
            null,
            null,
            null,
            AnnouncementStatus.Draft,
            DateTime.UtcNow);
    }

    #region GetAllAnnouncements

    [Fact]
    public async Task GetAllAnnouncements_WhenSuccess_ReturnsOkWithResponses()
    {
        List<AnnouncementDto> dtos = new()
        {
            CreateAnnouncementDto(title: "Announcement 1"),
            CreateAnnouncementDto(title: "Announcement 2")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetAllAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(dtos));

        IActionResult result = await _controller.GetAllAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAnnouncements_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetAllAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>([]));

        IActionResult result = await _controller.GetAllAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAnnouncements_MapsFieldsCorrectly()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        AnnouncementDto dto = new(id, "My Title", "My Content", AnnouncementType.Alert,
            AnnouncementTarget.All, null, null, null, true, false,
            "https://action.url", "Click Me", "https://img.url",
            AnnouncementStatus.Published, createdAt);
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetAllAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>(new List<AnnouncementDto> { dto }));

        IActionResult result = await _controller.GetAllAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        AnnouncementResponse response = responses[0];
        response.Id.Should().Be(id);
        response.Title.Should().Be("My Title");
        response.Content.Should().Be("My Content");
        response.Type.Should().Be("Alert");
        response.IsPinned.Should().BeTrue();
        response.IsDismissible.Should().BeFalse();
        response.ActionUrl.Should().Be("https://action.url");
        response.ActionLabel.Should().Be("Click Me");
        response.ImageUrl.Should().Be("https://img.url");
        response.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region CreateAnnouncement

    [Fact]
    public async Task CreateAnnouncement_WithValidRequest_Returns201Created()
    {
        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Feature);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateAnnouncement(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeOfType<AnnouncementResponse>();
    }

    [Fact]
    public async Task CreateAnnouncement_SanitizesTitleAndContent()
    {
        CreateAnnouncementRequest request = new("<script>Title</script>", "<b>Content</b>", AnnouncementType.Feature);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateAnnouncement(request, CancellationToken.None);

        _sanitizer.Received(1).Sanitize("<script>Title</script>");
        _sanitizer.Received(1).Sanitize("<b>Content</b>");
    }

    [Fact]
    public async Task CreateAnnouncement_WhenFailure_Returns400()
    {
        CreateAnnouncementRequest request = new("", "Content", AnnouncementType.Feature);
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnnouncementDto>(Error.Validation("Title is required")));

        IActionResult result = await _controller.CreateAnnouncement(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateAnnouncement_PassesAllFieldsToCommand()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        CreateAnnouncementRequest request = new(
            "Title", "Content", AnnouncementType.Alert,
            AnnouncementTarget.Tenant, "tenant-123",
            publishAt, expiresAt, true, false,
            "https://action.url", "Click", "https://img.url");
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateAnnouncement(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<AnnouncementDto>>(
            Arg.Is<CreateAnnouncementCommand>(c =>
                c.Type == AnnouncementType.Alert &&
                c.Target == AnnouncementTarget.Tenant &&
                c.TargetValue == "tenant-123" &&
                c.IsPinned &&
                !c.IsDismissible),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateAnnouncement

    [Fact]
    public async Task UpdateAnnouncement_WhenSuccess_ReturnsOkWithResponse()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("New Title", "New Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null);
        AnnouncementDto dto = CreateAnnouncementDto(id);
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<UpdateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.UpdateAnnouncement(id, request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<AnnouncementResponse>();
    }

    [Fact]
    public async Task UpdateAnnouncement_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null);
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<UpdateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnnouncementDto>(Error.NotFound("Announcement", id)));

        IActionResult result = await _controller.UpdateAnnouncement(id, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region PublishAnnouncement

    [Fact]
    public async Task PublishAnnouncement_WhenSuccess_Returns204NoContent()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.PublishAnnouncement(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PublishAnnouncement_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Announcement", id)));

        IActionResult result = await _controller.PublishAnnouncement(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PublishAnnouncement_PassesCorrectIdToCommand()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.PublishAnnouncement(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<PublishAnnouncementCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region ArchiveAnnouncement

    [Fact]
    public async Task ArchiveAnnouncement_WhenSuccess_Returns204NoContent()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<ArchiveAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.ArchiveAnnouncement(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ArchiveAnnouncement_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<ArchiveAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Announcement", id)));

        IActionResult result = await _controller.ArchiveAnnouncement(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ArchiveAnnouncement_PassesCorrectIdToCommand()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<ArchiveAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.ArchiveAnnouncement(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<ArchiveAnnouncementCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
