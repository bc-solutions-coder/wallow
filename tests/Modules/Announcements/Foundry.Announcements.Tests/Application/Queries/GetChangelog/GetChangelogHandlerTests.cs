using Foundry.Announcements.Application.Changelogs.DTOs;
using Foundry.Announcements.Application.Changelogs.Interfaces;
using Foundry.Announcements.Application.Changelogs.Queries.GetChangelog;
using Foundry.Announcements.Domain.Changelogs.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Queries.GetChangelog;

public class GetChangelogHandlerTests
{
    private readonly IChangelogRepository _repository = Substitute.For<IChangelogRepository>();
    private readonly GetChangelogHandler _handler;

    public GetChangelogHandlerTests()
    {
        _handler = new GetChangelogHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsPublishedEntries()
    {
        List<ChangelogEntry> entries =
        [
            ChangelogEntry.Create("1.0.0", "First", "Content", DateTime.UtcNow, TimeProvider.System),
            ChangelogEntry.Create("1.1.0", "Second", "Content", DateTime.UtcNow, TimeProvider.System)
        ];
        _repository.GetPublishedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

        GetChangelogQuery query = new();

        Result<IReadOnlyList<ChangelogEntryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsEmptyList()
    {
        _repository.GetPublishedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<ChangelogEntry>());

        GetChangelogQuery query = new();

        Result<IReadOnlyList<ChangelogEntryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesLimitToRepository()
    {
        _repository.GetPublishedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<ChangelogEntry>());

        GetChangelogQuery query = new(Limit: 25);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetPublishedAsync(25, Arg.Any<CancellationToken>());
    }
}
