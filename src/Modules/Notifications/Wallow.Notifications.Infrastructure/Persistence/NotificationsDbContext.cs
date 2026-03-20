using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : TenantAwareDbContext<NotificationsDbContext>
{
    // Email
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailPreference> EmailPreferences => Set<EmailPreference>();

    // SMS
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<SmsPreference> SmsPreferences => Set<SmsPreference>();

    // InApp Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Preferences
    public DbSet<ChannelPreference> ChannelPreferences => Set<ChannelPreference>();

    // Push
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<TenantPushConfiguration> TenantPushConfigurations => Set<TenantPushConfiguration>();
    public DbSet<PushMessage> PushMessages => Set<PushMessage>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
