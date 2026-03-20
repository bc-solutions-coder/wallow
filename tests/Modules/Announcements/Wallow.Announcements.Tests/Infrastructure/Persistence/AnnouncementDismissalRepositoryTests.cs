using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Announcements.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class AnnouncementDismissalRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<AnnouncementsDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private AnnouncementDismissalRepository CreateDismissalRepository() =>
        new(DbContext);

    private AnnouncementRepository CreateAnnouncementRepository() =>
        new(DbContext, TimeProvider.System);

    private async Task<Announcement> CreateAndSaveAnnouncement()
    {
        AnnouncementRepository announcementRepo = CreateAnnouncementRepository();
        Announcement announcement = Announcement.Create(
            TestTenantId, "Test", "Content", AnnouncementType.Feature, TimeProvider.System);
        await announcementRepo.AddAsync(announcement);
        return announcement;
    }

    [Fact]
    public async Task AddAsync_And_GetByUserIdAsync_ReturnsDismissal()
    {
        AnnouncementDismissalRepository repository = CreateDismissalRepository();
        Announcement announcement = await CreateAndSaveAnnouncement();
        UserId userId = UserId.New();
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcement.Id, userId, TimeProvider.System);

        await repository.AddAsync(dismissal);

        IReadOnlyList<AnnouncementDismissal> result = await repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(1);
        result[0].AnnouncementId.Should().Be(announcement.Id);
        result[0].UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyDismissalsForSpecificUser()
    {
        AnnouncementDismissalRepository repository = CreateDismissalRepository();
        Announcement announcement = await CreateAndSaveAnnouncement();
        UserId user1 = UserId.New();
        UserId user2 = UserId.New();

        AnnouncementDismissal dismissal1 = AnnouncementDismissal.Create(announcement.Id, user1, TimeProvider.System);
        AnnouncementDismissal dismissal2 = AnnouncementDismissal.Create(announcement.Id, user2, TimeProvider.System);

        await repository.AddAsync(dismissal1);
        await repository.AddAsync(dismissal2);

        IReadOnlyList<AnnouncementDismissal> result = await repository.GetByUserIdAsync(user1);

        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(user1);
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        AnnouncementDismissalRepository repository = CreateDismissalRepository();
        Announcement announcement = await CreateAndSaveAnnouncement();
        UserId userId = UserId.New();
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcement.Id, userId, TimeProvider.System);

        await repository.AddAsync(dismissal);

        bool result = await repository.ExistsAsync(announcement.Id, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        AnnouncementDismissalRepository repository = CreateDismissalRepository();

        bool result = await repository.ExistsAsync(AnnouncementId.New(), UserId.New());

        result.Should().BeFalse();
    }
}
