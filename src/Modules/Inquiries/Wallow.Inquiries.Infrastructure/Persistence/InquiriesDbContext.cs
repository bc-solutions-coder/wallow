using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContext : TenantAwareDbContext<InquiriesDbContext>
{
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<InquiryComment> InquiryComments => Set<InquiryComment>();

    public InquiriesDbContext(DbContextOptions<InquiriesDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inquiries");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
