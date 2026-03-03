using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Storage.Infrastructure.Persistence.Configurations;

public sealed class StorageBucketConfiguration : IEntityTypeConfiguration<StorageBucket>
{
    public void Configure(EntityTypeBuilder<StorageBucket> builder)
    {
        builder.ToTable("buckets");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(new StronglyTypedIdConverter<StorageBucketId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(b => b.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(b => b.Access)
            .HasColumnName("access")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.MaxFileSizeBytes)
            .HasColumnName("max_file_size_bytes")
            .IsRequired();

        builder.Property(b => b.AllowedContentTypes)
            .HasColumnName("allowed_content_types")
            .HasColumnType("jsonb");

        builder.OwnsOne(b => b.Retention, retention =>
        {
            retention.Property(r => r.Days)
                .HasColumnName("retention_days");

            retention.Property(r => r.Action)
                .HasColumnName("retention_action")
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        builder.Property(b => b.Versioning)
            .HasColumnName("versioning")
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.TenantId, b.Name }).IsUnique().HasDatabaseName("ix_storage_buckets_tenant_name");
    }
}
