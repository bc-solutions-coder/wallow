using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class SsoConfigurationConfiguration : IEntityTypeConfiguration<SsoConfiguration>
{
    public void Configure(EntityTypeBuilder<SsoConfiguration> builder)
    {
        builder.ToTable("sso_configurations");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => SsoConfigurationId.Create(value))
            .HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Protocol)
            .HasConversion<string>()
            .HasColumnName("protocol")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SamlEntityId)
            .HasColumnName("saml_entity_id")
            .HasMaxLength(500);

        builder.Property(s => s.SamlSsoUrl)
            .HasColumnName("saml_sso_url")
            .HasMaxLength(500);

        builder.Property(s => s.SamlSloUrl)
            .HasColumnName("saml_slo_url")
            .HasMaxLength(500);

        builder.Property(s => s.SamlCertificate)
            .HasColumnName("saml_certificate")
            .HasMaxLength(4000);

        builder.Property(s => s.SamlNameIdFormat)
            .HasConversion<string>()
            .HasColumnName("saml_name_id_format")
            .HasMaxLength(50);

        builder.Property(s => s.OidcIssuer)
            .HasColumnName("oidc_issuer")
            .HasMaxLength(500);

        builder.Property(s => s.OidcClientId)
            .HasColumnName("oidc_client_id")
            .HasMaxLength(200);

        builder.Property(s => s.OidcClientSecret)
            .HasColumnName("oidc_client_secret")
            .HasMaxLength(2000);

        builder.Property(s => s.OidcScopes)
            .HasColumnName("oidc_scopes")
            .HasMaxLength(500);

        builder.Property(s => s.EmailAttribute)
            .HasColumnName("email_attribute")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.FirstNameAttribute)
            .HasColumnName("first_name_attribute")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LastNameAttribute)
            .HasColumnName("last_name_attribute")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.GroupsAttribute)
            .HasColumnName("groups_attribute")
            .HasMaxLength(100);

        builder.Property(s => s.EnforceForAllUsers)
            .HasColumnName("enforce_for_all_users")
            .IsRequired();

        builder.Property(s => s.AutoProvisionUsers)
            .HasColumnName("auto_provision_users")
            .IsRequired();

        builder.Property(s => s.DefaultRole)
            .HasColumnName("default_role")
            .HasMaxLength(100);

        builder.Property(s => s.SyncGroupsAsRoles)
            .HasColumnName("sync_groups_as_roles")
            .IsRequired();

        builder.Property(s => s.IdpAlias)
            .HasColumnName("idp_alias")
            .HasMaxLength(200);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.IdpAlias)
            .IsUnique()
            .HasFilter("idp_alias IS NOT NULL");
    }
}
