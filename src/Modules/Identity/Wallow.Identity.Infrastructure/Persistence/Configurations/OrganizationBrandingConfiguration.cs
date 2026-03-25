using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationBrandingConfiguration : IEntityTypeConfiguration<OrganizationBranding>
{
    public void Configure(EntityTypeBuilder<OrganizationBranding> builder)
    {
        builder.ToTable("organization_branding");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => OrganizationBrandingId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => OrganizationId.Create(value))
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(e => e.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(2048);

        builder.Property(e => e.PrimaryColor)
            .HasColumnName("primary_color")
            .HasMaxLength(50);

        builder.Property(e => e.AccentColor)
            .HasColumnName("accent_color")
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Organization>()
            .WithOne()
            .HasForeignKey<OrganizationBranding>(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.OrganizationId).IsUnique();
        builder.HasIndex(e => e.TenantId);
    }
}
