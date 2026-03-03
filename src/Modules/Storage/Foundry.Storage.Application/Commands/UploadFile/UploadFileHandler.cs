using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Contracts.Storage.Commands;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Utilities;
using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Application.Commands.UploadFile;

public sealed class UploadFileHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider,
    IFileScanner fileScanner)
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
        string extension = System.IO.Path.GetExtension(sanitizedFileName);

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
