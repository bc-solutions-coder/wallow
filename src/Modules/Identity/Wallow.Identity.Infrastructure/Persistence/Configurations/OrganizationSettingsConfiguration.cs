using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("organization_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => OrganizationSettingsId.Create(value))
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

        builder.Property(e => e.RequireMfa)
            .HasColumnName("require_mfa")
            .IsRequired();

        builder.Property(e => e.AllowPasswordlessLogin)
            .HasColumnName("allow_passwordless_login")
            .IsRequired();

        builder.Property(e => e.MfaGracePeriodDays)
            .HasColumnName("mfa_grace_period_days")
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Organization>()
            .WithOne()
            .HasForeignKey<OrganizationSettings>(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.OrganizationId).IsUnique();
        builder.HasIndex(e => e.TenantId);
    }
}
