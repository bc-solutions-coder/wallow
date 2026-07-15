using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Announcements.Domain.Changelogs.Enums;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Announcements.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class ChangelogRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<AnnouncementsDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private ChangelogRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsEntry()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Initial Release", "First release", DateTime.UtcNow, TimeProvider.System);

        await repository.AddAsync(entry);

        ChangelogEntry? result = await repository.GetByIdAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0.0");
        result.Title.Should().Be("Initial Release");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        ChangelogRepository repository = CreateRepository();

        ChangelogEntry? result = await repository.GetByIdAsync(ChangelogEntryId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByVersionAsync_ReturnsMatchingEntry()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry entry = ChangelogEntry.Create("2.5.0", "Feature Release", "Many features", DateTime.UtcNow, TimeProvider.System);

        await repository.AddAsync(entry);

        ChangelogEntry? result = await repository.GetByVersionAsync("2.5.0");

        result.Should().NotBeNull();
        result!.Version.Should().Be("2.5.0");
    }

    [Fact]
    public async Task GetByVersionAsync_WhenNotExists_ReturnsNull()
    {
        ChangelogRepository repository = CreateRepository();

        ChangelogEntry? result = await repository.GetByVersionAsync("99.99.99");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublishedEntries()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry draft = ChangelogEntry.Create("3.0.0-draft", "Draft Entry", "Not published", DateTime.UtcNow, TimeProvider.System);
        ChangelogEntry published = ChangelogEntry.Create("3.0.0", "Published Entry", "Published content", DateTime.UtcNow, TimeProvider.System);
        published.Publish(TimeProvider.System);

        await repository.AddAsync(draft);
        await repository.AddAsync(published);

        IReadOnlyList<ChangelogEntry> result = await repository.GetPublishedAsync();

        result.Should().OnlyContain(e => e.IsPublished);
    }

    [Fact]
    public async Task GetLatestPublishedAsync_ReturnsNewestByReleasedAt()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry older = ChangelogEntry.Create("4.0.0", "Old Release", "Content", DateTime.UtcNow.AddDays(-10), TimeProvider.System);
        ChangelogEntry newer = ChangelogEntry.Create("4.1.0", "New Release", "Content", DateTime.UtcNow, TimeProvider.System);
        older.Publish(TimeProvider.System);
        newer.Publish(TimeProvider.System);

        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        ChangelogEntry? result = await repository.GetLatestPublishedAsync();

        result.Should().NotBeNull();
        result!.Version.Should().Be("4.1.0");
    }

    [Fact]
    public async Task UpdateAsync_ModifiesEntry()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry entry = ChangelogEntry.Create("5.0.0", "Original Title", "Content", DateTime.UtcNow, TimeProvider.System);

        await repository.AddAsync(entry);

        entry.Publish(TimeProvider.System);
        await repository.UpdateAsync(entry);

        ChangelogEntry? result = await repository.GetByIdAsync(entry.Id);

        result.Should().NotBeNull();
        result!.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesItems()
    {
        ChangelogRepository repository = CreateRepository();
        ChangelogEntry entry = ChangelogEntry.Create("6.0.0", "With Items", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.AddItem("Fixed a bug", ChangeType.Fix, TimeProvider.System);
        entry.AddItem("Added feature", ChangeType.Feature, TimeProvider.System);

        await repository.AddAsync(entry);

        ChangelogEntry? result = await repository.GetByIdAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
    }
}
