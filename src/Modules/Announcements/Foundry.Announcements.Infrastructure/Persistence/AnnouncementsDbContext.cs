using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Changelogs.Entities;
using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Announcements.Infrastructure.Persistence;

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
