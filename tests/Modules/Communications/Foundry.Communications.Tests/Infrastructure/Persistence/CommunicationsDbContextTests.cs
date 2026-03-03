using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Tests.Infrastructure.Persistence;

public sealed class CommunicationsDbContextTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;

    public CommunicationsDbContextTests()
    {
        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));

        _dbContext = new CommunicationsDbContext(options, tenantContext);
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        _dbContext.Should().NotBeNull();
    }

    [Fact]
    public void EmailMessages_DbSet_IsAccessible()
    {
        _dbContext.EmailMessages.Should().NotBeNull();
    }

    [Fact]
    public void EmailPreferences_DbSet_IsAccessible()
    {
        _dbContext.EmailPreferences.Should().NotBeNull();
    }

    [Fact]
    public void Notifications_DbSet_IsAccessible()
    {
        _dbContext.Notifications.Should().NotBeNull();
    }

    [Fact]
    public void Announcements_DbSet_IsAccessible()
    {
        _dbContext.Announcements.Should().NotBeNull();
    }

    [Fact]
    public void AnnouncementDismissals_DbSet_IsAccessible()
    {
        _dbContext.AnnouncementDismissals.Should().NotBeNull();
    }

    [Fact]
    public void ChangelogEntries_DbSet_IsAccessible()
    {
        _dbContext.ChangelogEntries.Should().NotBeNull();
    }

    [Fact]
    public void ChangelogItems_DbSet_IsAccessible()
    {
        _dbContext.ChangelogItems.Should().NotBeNull();
    }

    [Fact]
    public void OnModelCreating_SetsDefaultSchema()
    {
        string? schema = _dbContext.Model.GetDefaultSchema();
        schema.Should().Be("communications");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
