using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasConversion(new StronglyTypedIdConverter<StoredFileId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(f => f.BucketId)
            .HasConversion(new StronglyTypedIdConverter<StorageBucketId>())
            .HasColumnName("bucket_id")
            .IsRequired();

        builder.Property(f => f.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(f => f.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(f => f.Path)
            .HasColumnName("path")
            .HasMaxLength(500);

        builder.Property(f => f.IsPublic)
            .HasColumnName("is_public")
            .IsRequired();

        builder.Property(f => f.UploadedBy)
            .HasColumnName("uploaded_by")
            .IsRequired();

        builder.Property(f => f.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        builder.Property(f => f.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.HasOne<StorageBucket>()
            .WithMany()
            .HasForeignKey(f => f.BucketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => f.BucketId);
        builder.HasIndex(f => new { f.BucketId, f.Path });
        builder.HasIndex(f => f.StorageKey).IsUnique();
    }
}
