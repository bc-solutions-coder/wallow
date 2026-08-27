using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Application.Commands.CompletePresignedUpload;

public sealed class CompletePresignedUploadHandler(
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider,
    IFileScanner fileScanner)
{
    public async Task<Result<CompletePresignedUploadResult>> Handle(
        CompletePresignedUploadCommand command,
        CancellationToken cancellationToken)
    {
        StoredFile? storedFile = await fileRepository.GetByIdAsync(
            StoredFileId.Create(command.FileId), cancellationToken);
        if (storedFile is null)
        {
            return Result.Failure<CompletePresignedUploadResult>(
                Error.NotFound("File", command.FileId));
        }

        if (storedFile.Status != FileStatus.PendingValidation)
        {
            return new CompletePresignedUploadResult(storedFile.Id.Value, storedFile.Status);
        }

        bool objectExists = await storageProvider.ExistsAsync(storedFile.StorageKey, cancellationToken);
        if (!objectExists)
        {
            return Result.Failure<CompletePresignedUploadResult>(Error.Validation(
                "File.NotUploaded",
                "No object has been uploaded for this file's presigned URL yet."));
        }

        Stream fileStream = await storageProvider.DownloadAsync(storedFile.StorageKey, cancellationToken);
        await using (fileStream.ConfigureAwait(false))
        {
            FileScanResult scanResult = await fileScanner.ScanAsync(
                fileStream, storedFile.FileName, cancellationToken);
            if (scanResult.IsClean)
            {
                storedFile.MarkAsAvailable();
            }
            else
            {
                storedFile.MarkAsRejected();
            }
        }

        await fileRepository.SaveChangesAsync(cancellationToken);

        return new CompletePresignedUploadResult(storedFile.Id.Value, storedFile.Status);
    }
}
