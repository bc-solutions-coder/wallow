using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Notifications.Infrastructure.Persistence.Configurations;

public sealed class TenantPushConfigurationConfiguration : IEntityTypeConfiguration<TenantPushConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantPushConfiguration> builder)
    {
        builder.ToTable("tenant_push_configurations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<TenantPushConfigurationId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.Platform)
            .HasColumnName("platform")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EncryptedCredentials)
            .HasColumnName("encrypted_credentials")
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Platform })
            .IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
