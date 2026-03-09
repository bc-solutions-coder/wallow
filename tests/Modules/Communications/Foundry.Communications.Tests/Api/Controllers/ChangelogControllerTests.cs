using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetChangelog;
using Foundry.Communications.Application.Announcements.Queries.GetChangelogEntry;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class ChangelogControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ChangelogController _controller;

    public ChangelogControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new ChangelogController(_bus)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetChangelog

    [Fact]
    public async Task GetChangelog_WithEntries_ReturnsOkWithList()
    {
        ChangelogEntryDto dto = CreateChangelogDto();
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([dto]));

        IActionResult result = await _controller.GetChangelog(ct: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        responses.Should().HaveCount(1);
        responses[0].Version.Should().Be("1.0.0");
        responses[0].Title.Should().Be("Release 1.0");
    }

    [Fact]
    public async Task GetChangelog_WithEmptyList_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([]));

        IActionResult result = await _controller.GetChangelog(ct: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChangelog_PassesLimitToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([]));

        await _controller.GetChangelog(limit: 25, ct: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(
            Arg.Is<GetChangelogQuery>(q => q.Limit == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChangelog_MapsChangelogItemsCorrectly()
    {
        ChangelogItemDto item = new(Guid.NewGuid(), "Added new feature", ChangeType.Feature);
        ChangelogEntryDto dto = new(Guid.NewGuid(), "1.0.0", "Release", "Content", DateTime.UtcNow, true,
            [item], DateTime.UtcNow);
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ChangelogEntryDto>>([dto]));

        IActionResult result = await _controller.GetChangelog(ct: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ChangelogEntryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ChangelogEntryResponse>>().Subject;
        responses[0].Items.Should().HaveCount(1);
        responses[0].Items[0].Description.Should().Be("Added new feature");
        responses[0].Items[0].Type.Should().Be("Feature");
    }

    [Fact]
    public async Task GetChangelog_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(Arg.Any<GetChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<ChangelogEntryDto>>(new Error("SomeError", "Failed")));

        IActionResult result = await _controller.GetChangelog(ct: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region GetChangelogByVersion

    [Fact]
    public async Task GetChangelogByVersion_WhenFound_ReturnsOk()
    {
        ChangelogEntryDto dto = CreateChangelogDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetChangelogByVersion("1.0.0", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ChangelogEntryResponse response = ok.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetChangelogByVersion_WhenNotFound_Returns404()
    {
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ChangelogEntryDto>(Error.NotFound("Changelog", "9.9.9")));

        IActionResult result = await _controller.GetChangelogByVersion("9.9.9", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetChangelogByVersion_PassesVersionToQuery()
    {
        ChangelogEntryDto dto = CreateChangelogDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetChangelogByVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetChangelogByVersion("2.0.0", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ChangelogEntryDto>>(
            Arg.Is<GetChangelogByVersionQuery>(q => q.Version == "2.0.0"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetLatestChangelog

    [Fact]
    public async Task GetLatestChangelog_WhenFound_ReturnsOk()
    {
        ChangelogEntryDto dto = CreateChangelogDto();
        _bus.InvokeAsync<Result<ChangelogEntryDto>>(Arg.Any<GetLatestChangelogQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetLatestChangelog(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ChangelogEntryResponse response = ok.Value.Should().BeOfType<ChangelogEntryResponse>().Subject;
        response.Version.Should().Be("1.0.0");
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

    private static ChangelogEntryDto CreateChangelogDto()
    {
        return new ChangelogEntryDto(
            Guid.NewGuid(), "1.0.0", "Release 1.0", "Release content", DateTime.UtcNow, true,
            [], DateTime.UtcNow);
    }
}
