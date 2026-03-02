using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Queries.GetChangelog;
using Foundry.Communications.Application.Announcements.Queries.GetChangelogEntry;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Queries;

public class ChangelogMappingTests
{
    [Fact]
    public async Task GetChangelogHandler_MapsItemsCorrectly()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetChangelogHandler handler = new(repository);

        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.AddItem("Added login feature", ChangeType.Feature, TimeProvider.System);
        entry.AddItem("Fixed crash on startup", ChangeType.Fix, TimeProvider.System);

        repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry> { entry });

        Result<IReadOnlyList<ChangelogEntryDto>> result = await handler.Handle(new GetChangelogQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Items.Should().HaveCount(2);
        result.Value[0].Items[0].Description.Should().Be("Added login feature");
        result.Value[0].Items[0].Type.Should().Be(ChangeType.Feature);
        result.Value[0].Items[1].Description.Should().Be("Fixed crash on startup");
        result.Value[0].Items[1].Type.Should().Be(ChangeType.Fix);
    }

    [Fact]
    public async Task GetChangelogByVersionHandler_MapsItemsCorrectly()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetChangelogByVersionHandler handler = new(repository);

        ChangelogEntry entry = ChangelogEntry.Create("2.0.0", "Major Release", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.AddItem("Breaking API change", ChangeType.Breaking, TimeProvider.System);
        entry.AddItem("Security patch", ChangeType.Security, TimeProvider.System);
        entry.AddItem("Deprecated old endpoint", ChangeType.Deprecated, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        repository.GetByVersionAsync("2.0.0", Arg.Any<CancellationToken>())
            .Returns(entry);

        Result<ChangelogEntryDto> result = await handler.Handle(new GetChangelogByVersionQuery("2.0.0"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items[0].Type.Should().Be(ChangeType.Breaking);
        result.Value.Items[1].Type.Should().Be(ChangeType.Security);
        result.Value.Items[2].Type.Should().Be(ChangeType.Deprecated);
    }

    [Fact]
    public async Task GetLatestChangelogHandler_MapsItemsCorrectly()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetLatestChangelogHandler handler = new(repository);

        ChangelogEntry entry = ChangelogEntry.Create("3.0.0", "Latest", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.AddItem("Performance improvement", ChangeType.Improvement, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(entry);

        Result<ChangelogEntryDto> result = await handler.Handle(new GetLatestChangelogQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Description.Should().Be("Performance improvement");
        result.Value.Items[0].Type.Should().Be(ChangeType.Improvement);
    }

    [Fact]
    public async Task GetChangelogHandler_MapsIdAndCreatedAt()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetChangelogHandler handler = new(repository);

        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);

        repository.GetPublishedAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ChangelogEntry> { entry });

        Result<IReadOnlyList<ChangelogEntryDto>> result = await handler.Handle(new GetChangelogQuery(), CancellationToken.None);

        result.Value[0].Id.Should().Be(entry.Id.Value);
        result.Value[0].CreatedAt.Should().Be(entry.CreatedAt);
    }

    [Fact]
    public async Task GetChangelogByVersionHandler_MapsAllFields()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetChangelogByVersionHandler handler = new(repository);

        DateTime releasedAt = DateTime.UtcNow.AddDays(-1);
        ChangelogEntry entry = ChangelogEntry.Create("1.5.0", "Minor Release", "Bug fixes and improvements", releasedAt, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        repository.GetByVersionAsync("1.5.0", Arg.Any<CancellationToken>())
            .Returns(entry);

        Result<ChangelogEntryDto> result = await handler.Handle(new GetChangelogByVersionQuery("1.5.0"), CancellationToken.None);

        result.Value.Id.Should().Be(entry.Id.Value);
        result.Value.Version.Should().Be("1.5.0");
        result.Value.Title.Should().Be("Minor Release");
        result.Value.Content.Should().Be("Bug fixes and improvements");
        result.Value.ReleasedAt.Should().Be(releasedAt);
        result.Value.IsPublished.Should().BeTrue();
        result.Value.CreatedAt.Should().Be(entry.CreatedAt);
    }

    [Fact]
    public async Task GetLatestChangelogHandler_MapsAllFields()
    {
        IChangelogRepository repository = Substitute.For<IChangelogRepository>();
        GetLatestChangelogHandler handler = new(repository);

        DateTime releasedAt = DateTime.UtcNow;
        ChangelogEntry entry = ChangelogEntry.Create("4.0.0", "Latest Major", "Breaking changes", releasedAt, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(entry);

        Result<ChangelogEntryDto> result = await handler.Handle(new GetLatestChangelogQuery(), CancellationToken.None);

        result.Value.Id.Should().Be(entry.Id.Value);
        result.Value.Version.Should().Be("4.0.0");
        result.Value.Title.Should().Be("Latest Major");
        result.Value.Content.Should().Be("Breaking changes");
        result.Value.ReleasedAt.Should().Be(releasedAt);
        result.Value.IsPublished.Should().BeTrue();
        result.Value.CreatedAt.Should().Be(entry.CreatedAt);
    }
}
