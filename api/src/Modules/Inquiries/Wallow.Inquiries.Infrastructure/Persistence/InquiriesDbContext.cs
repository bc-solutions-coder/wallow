using Microsoft.EntityFrameworkCore;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Core.Persistence;

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
        modelBuilder.HasDefaultSchema(InquiriesModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
