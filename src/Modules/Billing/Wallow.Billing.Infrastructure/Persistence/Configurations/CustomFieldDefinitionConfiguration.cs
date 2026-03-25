using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.CustomFields.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Infrastructure.Persistence.Configurations;

public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("custom_field_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => CustomFieldDefinitionId.Create(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FieldKey)
            .HasColumnName("field_key")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.FieldType)
            .HasColumnName("field_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0);

        builder.Property(x => x.IsRequired)
            .HasColumnName("is_required")
            .HasDefaultValue(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.ValidationRulesJson)
            .HasColumnName("validation_rules")
            .HasColumnType("jsonb");

        builder.Property(x => x.OptionsJson)
            .HasColumnName("options")
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.FieldKey })
            .IsUnique()
            .HasDatabaseName("ix_custom_field_definitions_tenant_entity_key");

        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.IsActive })
            .HasDatabaseName("ix_custom_field_definitions_tenant_entity_active");

        builder.HasIndex(x => x.TenantId);

        builder.Ignore(x => x.DomainEvents);
    }
}
