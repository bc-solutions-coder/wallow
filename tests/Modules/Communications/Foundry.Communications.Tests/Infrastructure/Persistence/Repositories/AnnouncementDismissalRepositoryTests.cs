using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementDismissalRepositoryTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly AnnouncementDismissalRepository _repository;

    public AnnouncementDismissalRepositoryTests()
    {
        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new AnnouncementDismissalRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_AddsDismissalToDatabase()
    {
        AnnouncementId announcementId = AnnouncementId.New();
        UserId userId = UserId.Create(Guid.NewGuid());
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);

        await _repository.AddAsync(dismissal);

        int count = await _dbContext.AnnouncementDismissals.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsMatchingDismissals()
    {
        UserId userId = UserId.Create(Guid.NewGuid());
        UserId otherUserId = UserId.Create(Guid.NewGuid());

        AnnouncementDismissal dismissal1 = AnnouncementDismissal.Create(AnnouncementId.New(), userId, TimeProvider.System);
        AnnouncementDismissal dismissal2 = AnnouncementDismissal.Create(AnnouncementId.New(), userId, TimeProvider.System);
        AnnouncementDismissal other = AnnouncementDismissal.Create(AnnouncementId.New(), otherUserId, TimeProvider.System);

        await _dbContext.AnnouncementDismissals.AddRangeAsync(dismissal1, dismissal2, other);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<AnnouncementDismissal> result = await _repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        AnnouncementId announcementId = AnnouncementId.New();
        UserId userId = UserId.Create(Guid.NewGuid());
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);

        await _dbContext.AnnouncementDismissals.AddAsync(dismissal);
        await _dbContext.SaveChangesAsync();

        bool exists = await _repository.ExistsAsync(announcementId, userId);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        bool exists = await _repository.ExistsAsync(AnnouncementId.New(), UserId.Create(Guid.NewGuid()));

        exists.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
