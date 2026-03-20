using Wallow.Showcases.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Showcases.Infrastructure.Persistence;

public sealed class ShowcasesDbContext : DbContext
{
    public DbSet<Showcase> Showcases => Set<Showcase>();

    public ShowcasesDbContext(DbContextOptions<ShowcasesDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("showcases");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShowcasesDbContext).Assembly);
    }
}
