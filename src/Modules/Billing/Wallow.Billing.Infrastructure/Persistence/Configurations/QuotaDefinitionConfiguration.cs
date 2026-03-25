using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Infrastructure.Persistence.Configurations;

public sealed class QuotaDefinitionConfiguration : IEntityTypeConfiguration<QuotaDefinition>
{
    public void Configure(EntityTypeBuilder<QuotaDefinition> builder)
    {
        builder.ToTable("quota_definitions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id)
            .HasConversion(new StronglyTypedIdConverter<QuotaDefinitionId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(q => q.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(q => q.MeterCode)
            .HasColumnName("meter_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(q => q.PlanCode)
            .HasColumnName("plan_code")
            .HasMaxLength(50);

        builder.Property(q => q.Limit)
            .HasColumnName("limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(q => q.Period)
            .HasColumnName("period")
            .IsRequired();

        builder.Property(q => q.OnExceeded)
            .HasColumnName("on_exceeded")
            .IsRequired();

        builder.Property(q => q.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(q => q.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(q => q.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(q => q.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(q => q.TenantId);
        builder.HasIndex(q => q.MeterCode);
        builder.HasIndex(q => q.PlanCode);
        builder.HasIndex(q => new { q.TenantId, q.MeterCode }).IsUnique()
            .HasFilter("plan_code IS NULL");
    }
}
