using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Queries.GetChangelog;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Queries;

public class GetChangelogHandlerTests
{
    private readonly IChangelogRepository _repository;
    private readonly GetChangelogHandler _handler;

    public GetChangelogHandlerTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new GetChangelogHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsPublishedEntries()
    {
        ChangelogEntry entry1 = ChangelogEntry.Create("1.0.0", "Release 1", "Content 1", DateTime.UtcNow, TimeProvider.System);
        ChangelogEntry entry2 = ChangelogEntry.Create("2.0.0", "Release 2", "Content 2", DateTime.UtcNow, TimeProvider.System);

        _repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry> { entry1, entry2 });

        GetChangelogQuery query = new();

        Result<IReadOnlyList<ChangelogEntryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithCustomLimit_PassesLimit()
    {
        _repository.GetPublishedAsync(10, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry>());

        GetChangelogQuery query = new(Limit: 10);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetPublishedAsync(10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsEmptyList()
    {
        _repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry>());

        GetChangelogQuery query = new();

        Result<IReadOnlyList<ChangelogEntryDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        DateTime releasedAt = DateTime.UtcNow;
        ChangelogEntry entry = ChangelogEntry.Create("1.2.3", "Patch Release", "Bug fixes", releasedAt, TimeProvider.System);

        _repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry> { entry });

        GetChangelogQuery query = new();

        Result<IReadOnlyList<ChangelogEntryDto>> result = await _handler.Handle(query, CancellationToken.None);

        ChangelogEntryDto dto = result.Value[0];
        dto.Version.Should().Be("1.2.3");
        dto.Title.Should().Be("Patch Release");
        dto.Content.Should().Be("Bug fixes");
        dto.ReleasedAt.Should().Be(releasedAt);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry>());

        GetChangelogQuery query = new();

        await _handler.Handle(query, cts.Token);

        await _repository.Received(1).GetPublishedAsync(50, cts.Token);
    }
}
