using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext : TenantAwareDbContext<BillingDbContext>
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<MeterDefinition> MeterDefinitions => Set<MeterDefinition>();
    public DbSet<QuotaDefinition> QuotaDefinitions => Set<QuotaDefinition>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<TenantSettingEntity> TenantSettings => Set<TenantSettingEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();

    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        modelBuilder.ApplySettingsConfigurations();

        ApplyTenantQueryFilters(modelBuilder);
    }
}
