using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Shared.Infrastructure.Settings;

public sealed class TenantSettingEntityConfiguration : IEntityTypeConfiguration<TenantSettingEntity>
{
    public void Configure(EntityTypeBuilder<TenantSettingEntity> builder)
    {
        builder.ToTable("tenant_settings");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(new StronglyTypedIdConverter<TenantSettingId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.SettingKey)
            .HasColumnName("setting_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Value)
            .HasColumnName("value")
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(t => t.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(t => new { t.TenantId, t.ModuleKey, t.SettingKey }).IsUnique();
    }
}
