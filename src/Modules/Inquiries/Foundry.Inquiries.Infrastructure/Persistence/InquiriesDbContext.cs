using Foundry.Inquiries.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContext : DbContext
{
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    public InquiriesDbContext(DbContextOptions<InquiriesDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inquiries");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);
    }
}
