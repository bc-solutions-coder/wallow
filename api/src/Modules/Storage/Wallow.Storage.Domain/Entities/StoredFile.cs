using JetBrains.Annotations;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Events;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Domain.Entities;

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
    public FileStatus Status { get; private set; }

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
        StoredFile file = new StoredFile
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
            Metadata = metadata,
            Status = FileStatus.Available
        };

        file.RaiseDomainEvent(new FileUploadedEvent(file.Id, file.BucketId, tenantId));

        return file;
    }

    [UsedImplicitly]
    public void UpdateMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    [UsedImplicitly]
    public void SetPublic(bool isPublic)
    {
        IsPublic = isPublic;
    }

    public static StoredFile CreatePendingValidation(
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
        StoredFile file = new StoredFile
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
            Metadata = metadata,
            Status = FileStatus.PendingValidation
        };

        return file;
    }

    public void MarkAsAvailable()
    {
        Status = FileStatus.Available;
    }

    public void MarkAsRejected()
    {
        Status = FileStatus.Rejected;
    }

    public void MarkAsDeleted()
    {
        RaiseDomainEvent(new FileDeletedEvent(Id, BucketId, TenantId));
    }
}
