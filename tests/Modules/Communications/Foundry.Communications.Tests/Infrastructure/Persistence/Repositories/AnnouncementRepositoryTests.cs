using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;


namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementRepositoryTests : IDisposable
{
    private static readonly TenantId _testTenantId = TenantId.New();
    private readonly CommunicationsDbContext _dbContext;
    private readonly AnnouncementRepository _repository;

    public AnnouncementRepositoryTests()
    {
        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new AnnouncementRepository(_dbContext, TimeProvider.System);
    }

    [Fact]
    public async Task AddAsync_AddsAnnouncementToDatabase()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "Test", "Content", AnnouncementType.Feature, TimeProvider.System);

        await _repository.AddAsync(announcement);

        Announcement? found = await _dbContext.Announcements.FindAsync(announcement.Id);
        found.Should().NotBeNull();
        found.Title.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsAnnouncement()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "Existing", "Content", AnnouncementType.Alert, TimeProvider.System);
        await _dbContext.Announcements.AddAsync(announcement);
        await _dbContext.SaveChangesAsync();

        Announcement? result = await _repository.GetByIdAsync(announcement.Id);

        result.Should().NotBeNull();
        result.Title.Should().Be("Existing");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        Announcement? result = await _repository.GetByIdAsync(AnnouncementId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublishedNonExpired()
    {
        Announcement published = Announcement.Create(_testTenantId, "Published", "Content", AnnouncementType.Feature, TimeProvider.System);
        published.Publish(TimeProvider.System);

        Announcement draft = Announcement.Create(_testTenantId, "Draft", "Content", AnnouncementType.Feature, TimeProvider.System);

        Announcement expired = Announcement.Create(_testTenantId, "Expired", "Content", AnnouncementType.Feature, TimeProvider.System, expiresAt: DateTime.UtcNow.AddDays(-1));
        expired.Publish(TimeProvider.System);

        await _dbContext.Announcements.AddRangeAsync(published, draft, expired);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<Announcement> result = await _repository.GetPublishedAsync();

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Published");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllAnnouncementsOrderedByCreatedAt()
    {
        Announcement first = Announcement.Create(_testTenantId, "First", "Content", AnnouncementType.Feature, TimeProvider.System);
        Announcement second = Announcement.Create(_testTenantId, "Second", "Content", AnnouncementType.Alert, TimeProvider.System);

        await _dbContext.Announcements.AddRangeAsync(first, second);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<Announcement> result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAnnouncementInDatabase()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "Original", "Content", AnnouncementType.Feature, TimeProvider.System);
        await _dbContext.Announcements.AddAsync(announcement);
        await _dbContext.SaveChangesAsync();

        announcement.Update("Updated", "New Content", AnnouncementType.Alert,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null, TimeProvider.System);
        await _repository.UpdateAsync(announcement);

        Announcement? found = await _dbContext.Announcements.FindAsync(announcement.Id);
        found!.Title.Should().Be("Updated");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
