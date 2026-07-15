using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class ScimSyncLogConfiguration : IEntityTypeConfiguration<ScimSyncLog>
{
    public void Configure(EntityTypeBuilder<ScimSyncLog> builder)
    {
        builder.ToTable("scim_sync_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => ScimSyncLogId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.Operation)
            .HasColumnName("operation")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ResourceType)
            .HasColumnName("resource_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.InternalId)
            .HasColumnName("internal_id")
            .HasMaxLength(200);

        builder.Property(e => e.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(e => e.RequestBody)
            .HasColumnName("request_body");

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => new { e.TenantId, e.Timestamp });
    }
}
