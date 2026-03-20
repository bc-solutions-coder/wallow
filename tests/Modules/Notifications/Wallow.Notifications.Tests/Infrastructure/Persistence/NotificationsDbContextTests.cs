using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class NotificationsDbContextTests : IDisposable
{
    private readonly NotificationsDbContext _context;

    public NotificationsDbContextTests()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());

        DbContextOptions<NotificationsDbContext> options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new NotificationsDbContext(options, tenantContext);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DbContext_UseNoTrackingByDefault()
    {
        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public void DbContext_HasEmailMessagesDbSet()
    {
        _context.EmailMessages.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasEmailPreferencesDbSet()
    {
        _context.EmailPreferences.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasSmsMessagesDbSet()
    {
        _context.SmsMessages.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasSmsPreferencesDbSet()
    {
        _context.SmsPreferences.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasNotificationsDbSet()
    {
        _context.Notifications.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasChannelPreferencesDbSet()
    {
        _context.ChannelPreferences.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasDeviceRegistrationsDbSet()
    {
        _context.DeviceRegistrations.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasTenantPushConfigurationsDbSet()
    {
        _context.TenantPushConfigurations.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_HasPushMessagesDbSet()
    {
        _context.PushMessages.Should().NotBeNull();
    }
}
