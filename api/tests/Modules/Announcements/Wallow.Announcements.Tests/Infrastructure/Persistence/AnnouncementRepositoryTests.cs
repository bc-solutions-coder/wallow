using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Announcements.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class AnnouncementRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<AnnouncementsDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private AnnouncementRepository CreateRepository() =>
        new(DbContext, TimeProvider.System);

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsAnnouncement()
    {
        AnnouncementRepository repository = CreateRepository();
        Announcement announcement = Announcement.Create(
            TestTenantId, "Test Title", "Test Content",
            AnnouncementType.Feature, TimeProvider.System);

        await repository.AddAsync(announcement);

        Announcement? result = await repository.GetByIdAsync(announcement.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Title");
        result.Content.Should().Be("Test Content");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        AnnouncementRepository repository = CreateRepository();

        Announcement? result = await repository.GetByIdAsync(AnnouncementId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllAnnouncementsForTenant()
    {
        AnnouncementRepository repository = CreateRepository();
        Announcement a1 = Announcement.Create(TestTenantId, "Title 1", "Content", AnnouncementType.Feature, TimeProvider.System);
        Announcement a2 = Announcement.Create(TestTenantId, "Title 2", "Content", AnnouncementType.Alert, TimeProvider.System);

        await repository.AddAsync(a1);
        await repository.AddAsync(a2);

        IReadOnlyList<Announcement> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublishedAnnouncements()
    {
        AnnouncementRepository repository = CreateRepository();
        Announcement draft = Announcement.Create(TestTenantId, "Draft", "Content", AnnouncementType.Feature, TimeProvider.System);
        Announcement published = Announcement.Create(TestTenantId, "Published", "Content", AnnouncementType.Feature, TimeProvider.System);
        published.Publish(TimeProvider.System);

        await repository.AddAsync(draft);
        await repository.AddAsync(published);

        IReadOnlyList<Announcement> result = await repository.GetPublishedAsync();

        result.Should().OnlyContain(a => a.Status == AnnouncementStatus.Published);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesAnnouncement()
    {
        AnnouncementRepository repository = CreateRepository();
        Announcement announcement = Announcement.Create(
            TestTenantId, "Original Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        await repository.AddAsync(announcement);

        announcement.Publish(TimeProvider.System);
        await repository.UpdateAsync(announcement);

        Announcement? result = await repository.GetByIdAsync(announcement.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(AnnouncementStatus.Published);
    }

    [Fact]
    public async Task GetPublishedAsync_ExcludesExpiredAnnouncements()
    {
        AnnouncementRepository repository = CreateRepository();
        Announcement published = Announcement.Create(
            TestTenantId, "Published", "Content", AnnouncementType.Feature, TimeProvider.System,
            expiresAt: DateTime.UtcNow.AddDays(-1));
        published.Publish(TimeProvider.System);

        await repository.AddAsync(published);

        IReadOnlyList<Announcement> result = await repository.GetPublishedAsync();

        result.Should().NotContain(a => a.Title == "Published" && a.ExpiresAt < DateTime.UtcNow);
    }
}
