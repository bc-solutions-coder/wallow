using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Announcements.Commands.CreateChangelogEntry;
using Foundry.Communications.Application.Announcements.Commands.PublishChangelogEntry;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Infrastructure.Core.Services;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class AdminChangelogControllerTests
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;
    private readonly AdminChangelogController _controller;

    public AdminChangelogControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _sanitizer = Substitute.For<IHtmlSanitizationService>();
        _controller = new AdminChangelogController(_bus, _sanitizer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region CreateChangelogEntry

    [Fact]
    public async Task CreateChangelogEntry_WithValidRequest_Returns201Created()
    {
        DateTime releasedAt = DateTime.UtcNow;
        CreateChangelogEntryRequest request = new("1.0.0", "Release 1.0", "Content", releasedAt);
        ChangelogEntryDto dto = new(Guid.NewGuid(), "1.0.0", "Release 1.0", "Content", releasedAt, false,
            [], DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        ChangelogEntryResponse response = created.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Version.Should().Be("1.0.0");
        response.Title.Should().Be("Release 1.0");
    }

    [Fact]
    public async Task CreateChangelogEntry_PassesAllFieldsToCommand()
    {
        DateTime releasedAt = DateTime.UtcNow;
        CreateChangelogEntryRequest request = new("2.0.0", "Release 2.0", "New features", releasedAt);
        _sanitizer.Sanitize("Release 2.0").Returns("Release 2.0 sanitized");
        _sanitizer.Sanitize("New features").Returns("New features sanitized");
        ChangelogEntryDto dto = new(Guid.NewGuid(), "2.0.0", "Release 2.0 sanitized", "New features sanitized", releasedAt, false,
            [], DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateChangelogEntry(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ChangelogEntryDto>>(
            Arg.Is<CreateChangelogEntryCommand>(c =>
                c.Version == "2.0.0" &&
                c.Title == "Release 2.0 sanitized" &&
                c.Content == "New features sanitized" &&
                c.ReleasedAt == releasedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateChangelogEntry_WhenFailure_ReturnsErrorResult()
    {
        CreateChangelogEntryRequest request = new("1.0.0", "Release", "Content", DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ChangelogEntryDto>(Error.Validation("Version is required")));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateChangelogEntry_SetsLocationHeader()
    {
        CreateChangelogEntryRequest request = new("1.0.0", "Release", "Content", DateTime.UtcNow);
        ChangelogEntryDto dto = new(Guid.NewGuid(), "1.0.0", "Release", "Content", DateTime.UtcNow, false,
            [], DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/v1/admin/changelog");
    }

    [Fact]
    public async Task CreateChangelogEntry_MapsItemsInResponse()
    {
        DateTime releasedAt = DateTime.UtcNow;
        CreateChangelogEntryRequest request = new("1.0.0", "Release", "Content", releasedAt);
        ChangelogItemDto item = new(Guid.NewGuid(), "New feature added", ChangeType.Feature);
        ChangelogEntryDto dto = new(Guid.NewGuid(), "1.0.0", "Release", "Content", releasedAt, false,
            [item], DateTime.UtcNow);
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<CreateChangelogEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateChangelogEntry(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        ChangelogEntryResponse response = created.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Items.Should().HaveCount(1);
        response.Items[0].Description.Should().Be("New feature added");
        response.Items[0].Type.Should().Be("Feature");
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
    public async Task PublishChangelogEntry_PassesIdToCommand()
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
