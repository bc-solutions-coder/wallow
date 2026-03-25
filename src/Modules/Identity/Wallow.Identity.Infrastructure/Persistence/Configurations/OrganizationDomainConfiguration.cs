using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationDomainConfiguration : IEntityTypeConfiguration<OrganizationDomain>
{
    public void Configure(EntityTypeBuilder<OrganizationDomain> builder)
    {
        builder.ToTable("organization_domains");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => OrganizationDomainId.Create(value))
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

        builder.Property(e => e.Domain)
            .HasColumnName("domain")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.IsVerified)
            .HasColumnName("is_verified")
            .IsRequired();

        builder.Property(e => e.VerificationToken)
            .HasColumnName("verification_token")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.Domain).IsUnique();
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.TenantId);
    }
}
