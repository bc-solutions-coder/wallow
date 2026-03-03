using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Configuration.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagOverrideConfiguration : IEntityTypeConfiguration<FeatureFlagOverride>
{
    public void Configure(EntityTypeBuilder<FeatureFlagOverride> builder)
    {
        builder.ToTable("feature_flag_overrides");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => FeatureFlagOverrideId.Create(value))
            .IsRequired();

        builder.Property(o => o.FlagId)
            .HasColumnName("flag_id")
            .HasConversion(
                id => id.Value,
                value => FeatureFlagId.Create(value))
            .IsRequired();

        builder.Property(o => o.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(o => o.UserId)
            .HasColumnName("user_id");

        builder.Property(o => o.IsEnabled)
            .HasColumnName("is_enabled");

        builder.Property(o => o.Variant)
            .HasColumnName("variant")
            .HasMaxLength(100);

        builder.Property(o => o.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(o => new { o.FlagId, o.TenantId, o.UserId });
        builder.HasIndex(o => o.TenantId);
        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => new { o.TenantId, o.FlagId }).IsUnique().HasDatabaseName("ix_configuration_feature_flag_overrides_tenant_flag");
    }
}
