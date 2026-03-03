using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Application.Queries.GetUploadPresignedUrl;

public sealed class GetUploadPresignedUrlHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider)
{
    private static readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(15);

    public async Task<Result<PresignedUploadResult>> Handle(
        GetUploadPresignedUrlQuery query,
        CancellationToken cancellationToken)
    {
        StorageBucket? bucket = await bucketRepository.GetByNameAsync(query.BucketName, cancellationToken);
        if (bucket is null)
        {
            return Result.Failure<PresignedUploadResult>(
                Error.NotFound("Bucket", query.BucketName));
        }

        if (!bucket.IsContentTypeAllowed(query.ContentType))
        {
            return Result.Failure<PresignedUploadResult>(
                Error.Validation($"Content type '{query.ContentType}' is not allowed in bucket '{query.BucketName}'"));
        }

        if (!bucket.IsFileSizeAllowed(query.SizeBytes))
        {
            return Result.Failure<PresignedUploadResult>(
                Error.Validation($"File size {query.SizeBytes} bytes exceeds maximum allowed size of {bucket.MaxFileSizeBytes} bytes"));
        }

        Guid fileId = Guid.NewGuid();
        string extension = System.IO.Path.GetExtension(query.FileName);
        string storageKey = BuildStorageKey(query.TenantId, query.BucketName, query.Path, fileId, extension);

        TenantId tenantId = TenantId.Create(query.TenantId);
        StoredFile storedFile = StoredFile.Create(
            tenantId,
            bucket.Id,
            query.FileName,
            query.ContentType,
            query.SizeBytes,
            storageKey,
            query.UserId,
            query.Path);

        fileRepository.Add(storedFile);
        await fileRepository.SaveChangesAsync(cancellationToken);

        TimeSpan expiry = query.Expiry ?? _defaultExpiry;
        string url = await storageProvider.GetPresignedUrlAsync(
            storageKey,
            expiry,
            forUpload: true,
            cancellationToken);

        return new PresignedUploadResult(storedFile.Id.Value, url, DateTime.UtcNow.Add(expiry));
    }

    private static string BuildStorageKey(
        Guid tenantId,
        string bucketName,
        string? path,
        Guid fileId,
        string extension)
    {
        List<string> parts = new List<string>
        {
            $"tenant-{tenantId}",
            bucketName
        };

        if (!string.IsNullOrWhiteSpace(path))
        {
            parts.Add(path.Trim('/'));
        }

        parts.Add($"{fileId}{extension}");

        return string.Join("/", parts);
    }
}
