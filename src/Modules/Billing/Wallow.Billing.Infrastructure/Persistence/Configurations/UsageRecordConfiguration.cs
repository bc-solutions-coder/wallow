using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Infrastructure.Persistence.Configurations;

public sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_records");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(new StronglyTypedIdConverter<UsageRecordId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(u => u.MeterCode)
            .HasColumnName("meter_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(u => u.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(u => u.Value)
            .HasColumnName("value")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(u => u.FlushedAt)
            .HasColumnName("flushed_at")
            .IsRequired();

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => u.MeterCode);
        builder.HasIndex(u => u.PeriodStart);
        builder.HasIndex(u => new { u.TenantId, u.MeterCode, u.PeriodStart, u.PeriodEnd }).IsUnique();
    }
}
