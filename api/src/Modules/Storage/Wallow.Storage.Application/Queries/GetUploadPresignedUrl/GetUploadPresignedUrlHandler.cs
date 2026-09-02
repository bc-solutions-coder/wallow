using Microsoft.Extensions.Options;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Configuration;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Errors;

namespace Wallow.Storage.Application.Queries.GetUploadPresignedUrl;

public sealed class GetUploadPresignedUrlHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider,
    IOptions<PresignedUrlOptions> presignedUrlOptions,
    IStorageLimitsProvider limitsProvider)
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
                StorageErrors.BucketNotFound);
        }

        if (!bucket.IsContentTypeAllowed(query.ContentType))
        {
            return Result.Failure<PresignedUploadResult>(
                new Error(StorageErrors.FileContentTypeNotAllowed, $"Content type '{query.ContentType}' is not allowed in bucket '{query.BucketName}'"));
        }

        if (!bucket.IsFileSizeAllowed(query.SizeBytes))
        {
            return Result.Failure<PresignedUploadResult>(
                new Error(StorageErrors.FileTooLarge, $"File size {query.SizeBytes} bytes exceeds maximum allowed size of {bucket.MaxFileSizeBytes} bytes"));
        }

        Guid fileId = Guid.NewGuid();
        string extension = Path.GetExtension(query.FileName);

        StorageLimits limits = await limitsProvider.GetLimitsAsync(query.TenantId, cancellationToken);

        if (query.SizeBytes > limits.MaxUploadSizeBytes)
        {
            return Result.Failure<PresignedUploadResult>(
                new Error(StorageErrors.FileExceedsUploadLimit, $"File size {query.SizeBytes} bytes exceeds the tenant upload limit of {limits.MaxUploadSizeBytes} bytes"));
        }

        if (!limits.IsExtensionAllowed(extension))
        {
            return Result.Failure<PresignedUploadResult>(
                new Error(StorageErrors.FileExtensionNotAllowed, $"File extension '{extension}' is not allowed for this tenant"));
        }

        long usedBytes = await fileRepository.GetTotalSizeBytesAsync(cancellationToken);
        if (usedBytes + query.SizeBytes > limits.QuotaBytes)
        {
            return Result.Failure<PresignedUploadResult>(
                new Error(StorageErrors.QuotaExceeded, $"Upload of {query.SizeBytes} bytes would exceed the tenant storage quota of {limits.QuotaBytes} bytes"));
        }

        string storageKey = BuildStorageKey(query.TenantId, query.BucketName, query.Path, fileId, extension);

        TenantId tenantId = TenantId.Create(query.TenantId);
        StoredFile storedFile = StoredFile.CreatePendingValidation(
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

        TimeSpan maxExpiry = TimeSpan.FromMinutes(presignedUrlOptions.Value.MaxUploadExpiryMinutes);
        TimeSpan expiry = query.Expiry ?? _defaultExpiry;
        if (expiry > maxExpiry)
        {
            expiry = maxExpiry;
        }

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
        List<string> parts =
        [
            $"tenant-{tenantId}",
            bucketName
        ];

        if (!string.IsNullOrWhiteSpace(path))
        {
            parts.Add(path.Trim('/'));
        }

        parts.Add($"{fileId}{extension}");

        return string.Join("/", parts);
    }
}
