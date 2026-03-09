using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Announcements.Commands.ArchiveAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.PublishAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.UpdateAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetAllAnnouncements;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Infrastructure.Core.Services;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class AdminAnnouncementsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;
    private readonly AdminAnnouncementsController _controller;

    public AdminAnnouncementsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _sanitizer = Substitute.For<IHtmlSanitizationService>();
        _sanitizer.Sanitize(Arg.Any<string>()).Returns(x => x.Arg<string>());

        _controller = new AdminAnnouncementsController(_bus, _sanitizer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetAllAnnouncements

    [Fact]
    public async Task GetAllAnnouncements_ReturnsOkWithList()
    {
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(Arg.Any<GetAllAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AnnouncementDto>>([dto]));

        IActionResult result = await _controller.GetAllAnnouncements(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<AnnouncementResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<AnnouncementResponse>>().Subject;
        responses.Should().HaveCount(1);
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
        AnnouncementResponse response = created.Value.Should().BeOfType<AnnouncementResponse>().Subject;
        response.Title.Should().Be("Test Title");
    }

    [Fact]
    public async Task CreateAnnouncement_SanitizesTitleAndContent()
    {
        CreateAnnouncementRequest request = new("<script>alert('xss')</script>Title", "<b>Content</b>", AnnouncementType.Feature);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateAnnouncement(request, CancellationToken.None);

        _sanitizer.Received(1).Sanitize("<script>alert('xss')</script>Title");
        _sanitizer.Received(1).Sanitize("<b>Content</b>");
    }

    [Fact]
    public async Task CreateAnnouncement_PassesAllFieldsToCommand()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);
        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Alert,
            AnnouncementTarget.Plan, "Pro", publishAt, expiresAt, true, false,
            "https://example.com", "Action", "https://img.example.com/image.png");
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateAnnouncement(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<AnnouncementDto>>(
            Arg.Is<CreateAnnouncementCommand>(c =>
                c.Type == AnnouncementType.Alert &&
                c.Target == AnnouncementTarget.Plan &&
                c.TargetValue == "Pro" &&
                c.PublishAt == publishAt &&
                c.ExpiresAt == expiresAt &&
                c.IsPinned &&
                !c.IsDismissible &&
                c.ActionUrl == "https://example.com" &&
                c.ActionLabel == "Action" &&
                c.ImageUrl == "https://img.example.com/image.png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAnnouncement_WhenFailure_ReturnsErrorResult()
    {
        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Feature);
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AnnouncementDto>(Error.Validation("Title is required")));

        IActionResult result = await _controller.CreateAnnouncement(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateAnnouncement_SetsLocationHeader()
    {
        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Feature);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateAnnouncement(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/v1/admin/announcements");
    }

    #endregion

    #region UpdateAnnouncement

    [Fact]
    public async Task UpdateAnnouncement_WhenSuccess_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("Updated", "New Content", AnnouncementType.Update,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<UpdateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.UpdateAnnouncement(id, request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<AnnouncementResponse>();
    }

    [Fact]
    public async Task UpdateAnnouncement_SanitizesTitleAndContent()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("<b>Title</b>", "<script>x</script>", AnnouncementType.Update,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<UpdateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.UpdateAnnouncement(id, request, CancellationToken.None);

        _sanitizer.Received(1).Sanitize("<b>Title</b>");
        _sanitizer.Received(1).Sanitize("<script>x</script>");
    }

    [Fact]
    public async Task UpdateAnnouncement_PassesIdToCommand()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("T", "C", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null);
        AnnouncementDto dto = CreateAnnouncementDto();
        _bus.InvokeAsync<Result<AnnouncementDto>>(Arg.Any<UpdateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.UpdateAnnouncement(id, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<AnnouncementDto>>(
            Arg.Is<UpdateAnnouncementCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAnnouncement_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        UpdateAnnouncementRequest request = new("T", "C", AnnouncementType.Feature,
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
    public async Task PublishAnnouncement_PassesIdToCommand()
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
    public async Task ArchiveAnnouncement_PassesIdToCommand()
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

    private static AnnouncementDto CreateAnnouncementDto()
    {
        return new AnnouncementDto(
            Guid.NewGuid(), "Test Title", "Test Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, true, true, null, null, null,
            AnnouncementStatus.Published, DateTime.UtcNow);
    }
}
