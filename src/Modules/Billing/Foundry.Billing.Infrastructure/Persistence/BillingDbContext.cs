using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Shared.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext : TenantAwareDbContext<BillingDbContext>
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<MeterDefinition> MeterDefinitions => Set<MeterDefinition>();
    public DbSet<QuotaDefinition> QuotaDefinitions => Set<QuotaDefinition>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    public BillingDbContext(DbContextOptions<BillingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
