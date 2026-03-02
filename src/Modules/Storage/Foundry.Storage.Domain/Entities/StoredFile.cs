using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Domain.Entities;

/// <summary>
/// Metadata for a stored file. Actual bytes live in the storage backend.
/// Tenant-scoped to ensure proper isolation.
/// </summary>
public sealed class StoredFile : AggregateRoot<StoredFileId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public StorageBucketId BucketId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public string? Path { get; private set; }
    public bool IsPublic { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public string? Metadata { get; private set; }

    private StoredFile() { }

    public static StoredFile Create(
        TenantId tenantId,
        StorageBucketId bucketId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid uploadedBy,
        string? path = null,
        bool isPublic = false,
        string? metadata = null)
    {
        return new StoredFile
        {
            Id = StoredFileId.New(),
            TenantId = tenantId,
            BucketId = bucketId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Path = path,
            IsPublic = isPublic,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            Metadata = metadata
        };
    }

    public void UpdateMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    public void SetPublic(bool isPublic)
    {
        IsPublic = isPublic;
    }
}
