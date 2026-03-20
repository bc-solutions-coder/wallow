using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Api.Controllers;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelog;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelogEntry;
using Wallow.Announcements.Domain.Changelogs.Enums;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Announcements.Tests.Api.Controllers;

public class ChangelogControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ChangelogController _controller;

    public ChangelogControllerTests()
    {
        _controller = new ChangelogController(_bus);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static ChangelogEntryDto CreateChangelogEntryDto(Guid? id = null, string version = "1.0.0")
    {
        return new ChangelogEntryDto(
            id ?? Guid.NewGuid(), version, "Release Notes", "Content",
            DateTime.UtcNow, true, [], DateTime.UtcNow);
    }

    #region GetChangelog

    [Fact]
    public async Task GetChangelog_WhenSuccess_ReturnsOkWithEntries()
    {
        List<ChangelogEntryDto> dtos = new()
        {
            CreateChangelogEntryDto(version: "2.0.0"),
            CreateChangelogEntryDto(version: "1.0.0")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>(dtos));

        IActionResult result = await _controller.GetChangelog(50, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChangelog_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([]));

        IActionResult result = await _controller.GetChangelog(50, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChangelog_PassesLimitToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([]));

        await _controller.GetChangelog(10, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(
            Arg.Is<GetChangelogQuery>(q => q.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChangelog_MapsFieldsCorrectly()
    {
        Guid id = Guid.NewGuid();
        DateTime releasedAt = DateTime.UtcNow;
        List<ChangelogItemDto> items = new()
        {
            new ChangelogItemDto(Guid.NewGuid(), "Fixed something", ChangeType.Fix)
        };
        ChangelogEntryDto dto = new(id, "3.0.0", "Major Release", "Major changes", releasedAt, true, items, DateTime.UtcNow);
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>(new List<ChangelogEntryDto> { dto }));

        IActionResult result = await _controller.GetChangelog(50, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        ChangelogEntryResponse response = responses[0];
        response.Id.Should().Be(id);
        response.Version.Should().Be("3.0.0");
        response.Title.Should().Be("Major Release");
        response.Content.Should().Be("Major changes");
        response.ReleasedAt.Should().Be(releasedAt);
        response.Items.Should().HaveCount(1);
        response.Items[0].Description.Should().Be("Fixed something");
        response.Items[0].Type.Should().Be("Fix");
    }

    #endregion

    #region GetChangelogByVersion

    [Fact]
    public async Task GetChangelogByVersion_WhenFound_ReturnsOkWithResponse()
    {
        ChangelogEntryDto dto = CreateChangelogEntryDto(version: "1.5.0");
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetChangelogByVersion("1.5.0", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ChangelogEntryResponse>();
    }

    [Fact]
    public async Task GetChangelogByVersion_WhenNotFound_Returns404()
    {
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ChangelogEntryDto>(Error.NotFound("Changelog", "99.0.0")));

        IActionResult result = await _controller.GetChangelogByVersion("99.0.0", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetChangelogByVersion_PassesVersionToQuery()
    {
        ChangelogEntryDto dto = CreateChangelogEntryDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetChangelogByVersion("2.3.1", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ChangelogEntryDto>>(
            Arg.Is<GetChangelogByVersionQuery>(q => q.Version == "2.3.1"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetLatestChangelog

    [Fact]
    public async Task GetLatestChangelog_WhenFound_ReturnsOkWithResponse()
    {
        ChangelogEntryDto dto = CreateChangelogEntryDto(version: "5.0.0");
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetLatestChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetLatestChangelog(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ChangelogEntryResponse response = ok.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Version.Should().Be("5.0.0");
    }

    [Fact]
    public async Task GetLatestChangelog_WhenNotFound_Returns404()
    {
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetLatestChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ChangelogEntryDto>(Error.NotFound("Changelog", "latest")));

        IActionResult result = await _controller.GetLatestChangelog(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion
}
