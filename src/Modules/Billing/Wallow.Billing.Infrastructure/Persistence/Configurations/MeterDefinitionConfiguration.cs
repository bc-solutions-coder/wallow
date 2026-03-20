using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Billing.Infrastructure.Persistence.Configurations;

public sealed class MeterDefinitionConfiguration : IEntityTypeConfiguration<MeterDefinition>
{
    public void Configure(EntityTypeBuilder<MeterDefinition> builder)
    {
        builder.ToTable("meter_definitions");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(new StronglyTypedIdConverter<MeterDefinitionId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Unit)
            .HasColumnName("unit")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Aggregation)
            .HasColumnName("aggregation")
            .IsRequired();

        builder.Property(m => m.IsBillable)
            .HasColumnName("is_billable")
            .IsRequired();

        builder.Property(m => m.ValkeyKeyPattern)
            .HasColumnName("valkey_key_pattern")
            .HasMaxLength(500);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(m => m.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(m => m.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(m => m.Code).IsUnique();
    }
}
