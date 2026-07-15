using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Api.Controllers;
using Wallow.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;
using Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Domain.Changelogs.Enums;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Announcements.Tests.Api.Controllers;

public class AdminChangelogControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IHtmlSanitizationService _sanitizer = Substitute.For<IHtmlSanitizationService>();
    private readonly AdminChangelogController _controller;

    public AdminChangelogControllerTests()
    {
        _sanitizer.Sanitize(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        _controller = new AdminChangelogController(_bus, _sanitizer);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static ChangelogEntryDto CreateChangelogEntryDto(Guid? id = null, string version = "1.0.0")
    {
        return new ChangelogEntryDto(
            id ?? Guid.NewGuid(), version, "Release Notes", "Content",
            DateTime.UtcNow, false, [], DateTime.UtcNow);
    }

    #region CreateChangelogEntry

    [Fact]
    public async Task CreateChangelogEntry_WithValidRequest_Returns201Created()
    {
        CreateChangelogEntryRequest request = new("1.0.0", "Release Notes", "Content", DateTime.UtcNow);
        ChangelogEntryDto dto = CreateChangelogEntryDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeOfType<ChangelogEntryResponse>();
    }

    [Fact]
    public async Task CreateChangelogEntry_SanitizesTitleAndContent()
    {
        CreateChangelogEntryRequest request = new("1.0.0", "<b>Title</b>", "<script>Bad</script>", DateTime.UtcNow);
        ChangelogEntryDto dto = CreateChangelogEntryDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateChangelogEntry(request, CancellationToken.None);

        _sanitizer.Received(1).Sanitize("<b>Title</b>");
        _sanitizer.Received(1).Sanitize("<script>Bad</script>");
    }

    [Fact]
    public async Task CreateChangelogEntry_WhenFailure_Returns400()
    {
        CreateChangelogEntryRequest request = new("", "Title", "Content", DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ChangelogEntryDto>(Error.Validation("Version is required")));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateChangelogEntry_MapsResponseWithItems()
    {
        List<ChangelogItemDto> items = new()
        {
            new ChangelogItemDto(Guid.NewGuid(), "Fixed bug", ChangeType.Fix),
            new ChangelogItemDto(Guid.NewGuid(), "Added feature", ChangeType.Feature)
        };
        ChangelogEntryDto dto = new(Guid.NewGuid(), "2.0.0", "Release", "Content", DateTime.UtcNow, false, items, DateTime.UtcNow);
        CreateChangelogEntryRequest request = new("2.0.0", "Release", "Content", DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        ChangelogEntryResponse response = created.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Items.Should().HaveCount(2);
        response.Items[0].Description.Should().Be("Fixed bug");
        response.Items[0].Type.Should().Be("Fix");
    }

    [Fact]
    public async Task CreateChangelogEntry_PassesCorrectFieldsToCommand()
    {
        DateTime releasedAt = DateTime.UtcNow;
        CreateChangelogEntryRequest request = new("1.5.0", "Release", "Content", releasedAt);
        ChangelogEntryDto dto = CreateChangelogEntryDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateChangelogEntry(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ChangelogEntryDto>>(
            Arg.Is<CreateChangelogEntryCommand>(c =>
                c.Version == "1.5.0" &&
                c.ReleasedAt == releasedAt),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region PublishChangelogEntry

    [Fact]
    public async Task PublishChangelogEntry_WhenSuccess_Returns204NoContent()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.PublishChangelogEntry(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PublishChangelogEntry_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("ChangelogEntry", id)));

        IActionResult result = await _controller.PublishChangelogEntry(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PublishChangelogEntry_PassesCorrectIdToCommand()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<PublishChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.PublishChangelogEntry(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<PublishChangelogEntryCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
