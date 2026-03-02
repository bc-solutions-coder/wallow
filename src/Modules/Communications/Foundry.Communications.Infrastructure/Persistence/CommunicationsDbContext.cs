using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Shared.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence;

public sealed class CommunicationsDbContext : TenantAwareDbContext<CommunicationsDbContext>
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

    // Messaging
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Participant> Participants => Set<Participant>();

    // Announcements
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementDismissal> AnnouncementDismissals => Set<AnnouncementDismissal>();
    public DbSet<ChangelogEntry> ChangelogEntries => Set<ChangelogEntry>();
    public DbSet<ChangelogItem> ChangelogItems => Set<ChangelogItem>();

    public CommunicationsDbContext(
        DbContextOptions<CommunicationsDbContext> options,
        ITenantContext tenantContext) : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("communications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationsDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
