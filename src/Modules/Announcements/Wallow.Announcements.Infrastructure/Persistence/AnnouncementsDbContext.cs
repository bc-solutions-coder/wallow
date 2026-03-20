using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Announcements.Infrastructure.Persistence;

public sealed class AnnouncementsDbContext : TenantAwareDbContext<AnnouncementsDbContext>
{
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementDismissal> AnnouncementDismissals => Set<AnnouncementDismissal>();
    public DbSet<ChangelogEntry> ChangelogEntries => Set<ChangelogEntry>();
    public DbSet<ChangelogItem> ChangelogItems => Set<ChangelogItem>();

    public AnnouncementsDbContext(DbContextOptions<AnnouncementsDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("announcements");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnnouncementsDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
