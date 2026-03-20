using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Shared.Infrastructure.Settings;

public sealed class UserSettingEntityConfiguration : IEntityTypeConfiguration<UserSettingEntity>
{
    public void Configure(EntityTypeBuilder<UserSettingEntity> builder)
    {
        builder.ToTable("user_settings");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(new StronglyTypedIdConverter<UserSettingId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(u => u.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.SettingKey)
            .HasColumnName("setting_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Value)
            .HasColumnName("value")
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(u => u.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(u => new { u.TenantId, u.UserId, u.ModuleKey, u.SettingKey }).IsUnique();
    }
}
