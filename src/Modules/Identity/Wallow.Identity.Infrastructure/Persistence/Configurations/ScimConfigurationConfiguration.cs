using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class ScimConfigurationConfiguration : IEntityTypeConfiguration<ScimConfiguration>
{
    public void Configure(EntityTypeBuilder<ScimConfiguration> builder)
    {
        builder.ToTable("scim_configurations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => ScimConfigurationId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(e => e.BearerToken)
            .HasColumnName("bearer_token")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.TokenPrefix)
            .HasColumnName("token_prefix")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.TokenExpiresAt)
            .HasColumnName("token_expires_at")
            .IsRequired();

        builder.Property(e => e.LastSyncAt)
            .HasColumnName("last_sync_at");

        builder.Property(e => e.AutoActivateUsers)
            .HasColumnName("auto_activate_users")
            .IsRequired();

        builder.Property(e => e.DefaultRole)
            .HasColumnName("default_role")
            .HasMaxLength(100);

        builder.Property(e => e.DeprovisionOnDelete)
            .HasColumnName("deprovision_on_delete")
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(e => e.TenantId).IsUnique();
    }
}
