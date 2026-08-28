using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Contracts.Storage.Commands;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Application.Utilities;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Application.Commands.UploadFile;

public sealed class UploadFileHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider,
    IFileScanner fileScanner,
    IStorageLimitsProvider limitsProvider)
{
    public async Task<Result<UploadResult>> Handle(
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        StorageBucket? bucket = await bucketRepository.GetByNameAsync(command.BucketName, cancellationToken);
        if (bucket is null)
        {
            return Result.Failure<UploadResult>(
                Error.NotFound("Bucket", command.BucketName));
        }

        if (!bucket.IsContentTypeAllowed(command.ContentType))
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"Content type '{command.ContentType}' is not allowed in bucket '{command.BucketName}'"));
        }

        if (!bucket.IsFileSizeAllowed(command.SizeBytes))
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"File size {command.SizeBytes} bytes exceeds maximum allowed size of {bucket.MaxFileSizeBytes} bytes"));
        }

        TenantId tenantId = TenantId.Create(command.TenantId);
        Guid fileId = Guid.NewGuid();
        string sanitizedFileName = FileNameSanitizer.Sanitize(command.FileName);
        string extension = Path.GetExtension(sanitizedFileName);

        StorageLimits limits = await limitsProvider.GetLimitsAsync(command.TenantId, cancellationToken);

        if (command.SizeBytes > limits.MaxUploadSizeBytes)
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"File size {command.SizeBytes} bytes exceeds the tenant upload limit of {limits.MaxUploadSizeBytes} bytes"));
        }

        if (!limits.IsExtensionAllowed(extension))
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"File extension '{extension}' is not allowed for this tenant"));
        }

        long usedBytes = await fileRepository.GetTotalSizeBytesAsync(cancellationToken);
        if (usedBytes + command.SizeBytes > limits.QuotaBytes)
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"Upload of {command.SizeBytes} bytes would exceed the tenant storage quota of {limits.QuotaBytes} bytes"));
        }

        string storageKey = BuildStorageKey(
            command.TenantId,
            command.BucketName,
            command.Path,
            fileId,
            extension);

        FileScanResult scanResult = await fileScanner.ScanAsync(command.Content, command.FileName, cancellationToken);
        if (!scanResult.IsClean)
        {
            return Result.Failure<UploadResult>(
                Error.Validation($"File '{command.FileName}' failed security scan: {scanResult.ThreatName}"));
        }

        await storageProvider.UploadAsync(
            command.Content,
            storageKey,
            command.ContentType,
            cancellationToken);

        StoredFile storedFile = StoredFile.Create(
            tenantId,
            bucket.Id,
            sanitizedFileName,
            command.ContentType,
            command.SizeBytes,
            storageKey,
            command.UserId,
            command.Path,
            command.IsPublic,
            command.Metadata);

        fileRepository.Add(storedFile);
        await fileRepository.SaveChangesAsync(cancellationToken);

        return new UploadResult(
            storedFile.Id.Value,
            storedFile.FileName,
            storedFile.StorageKey,
            storedFile.SizeBytes,
            storedFile.ContentType,
            storedFile.UploadedAt);
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
