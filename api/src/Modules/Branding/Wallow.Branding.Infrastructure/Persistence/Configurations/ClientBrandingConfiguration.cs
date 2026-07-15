using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Persistence.Configurations;

public sealed class ClientBrandingConfiguration : IEntityTypeConfiguration<ClientBranding>
{
    public void Configure(EntityTypeBuilder<ClientBranding> builder)
    {
        builder.ToTable("client_brandings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => ClientBrandingId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Tagline)
            .HasColumnName("tagline")
            .HasMaxLength(500);

        builder.Property(e => e.LogoStorageKey)
            .HasColumnName("logo_storage_key")
            .HasMaxLength(500);

        builder.Property(e => e.ThemeJson)
            .HasColumnName("theme_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.ClientId)
            .IsUnique();

        builder.HasIndex(e => e.TenantId);
    }
}
