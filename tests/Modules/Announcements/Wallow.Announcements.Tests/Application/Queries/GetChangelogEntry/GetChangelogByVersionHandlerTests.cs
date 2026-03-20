using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelogEntry;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Tests.Application.Queries.GetChangelogEntry;

public class GetChangelogByVersionHandlerTests
{
    private readonly IChangelogRepository _repository = Substitute.For<IChangelogRepository>();
    private readonly GetChangelogByVersionHandler _handler;

    public GetChangelogByVersionHandlerTests()
    {
        _handler = new GetChangelogByVersionHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenPublishedEntryExists_ReturnsSuccess()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.Publish(TimeProvider.System);
        _repository.GetByVersionAsync("1.0.0", Arg.Any<CancellationToken>()).Returns(entry);

        GetChangelogByVersionQuery query = new("1.0.0");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        _repository.GetByVersionAsync("2.0.0", Arg.Any<CancellationToken>()).Returns((ChangelogEntry?)null);

        GetChangelogByVersionQuery query = new("2.0.0");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Changelog.NotFound");
    }

    [Fact]
    public async Task Handle_WhenEntryExistsButNotPublished_ReturnsNotFoundError()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        _repository.GetByVersionAsync("1.0.0", Arg.Any<CancellationToken>()).Returns(entry);

        GetChangelogByVersionQuery query = new("1.0.0");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Changelog.NotFound");
    }
}

public class GetLatestChangelogHandlerTests
{
    private readonly IChangelogRepository _repository = Substitute.For<IChangelogRepository>();
    private readonly GetLatestChangelogHandler _handler;

    public GetLatestChangelogHandlerTests()
    {
        _handler = new GetLatestChangelogHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenEntryExists_ReturnsSuccess()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.Publish(TimeProvider.System);
        _repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>()).Returns(entry);

        GetLatestChangelogQuery query = new();

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsNotFoundError()
    {
        _repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>()).Returns((ChangelogEntry?)null);

        GetLatestChangelogQuery query = new();

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Changelog.NotFound");
    }
}
