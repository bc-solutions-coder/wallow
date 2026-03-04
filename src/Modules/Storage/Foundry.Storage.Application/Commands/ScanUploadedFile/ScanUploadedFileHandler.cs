using Foundry.Shared.Contracts.Storage;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Foundry.Storage.Application.Commands.ScanUploadedFile;

public static partial class ScanUploadedFileHandler
{
    public static async Task HandleAsync(
        ScanUploadedFileCommand command,
        IStoredFileRepository fileRepository,
        IStorageProvider storageProvider,
        IFileScanner fileScanner,
        ILogger<ScanUploadedFileCommand> logger,
        CancellationToken cancellationToken = default)
    {
        StoredFile? storedFile = await fileRepository.GetByIdAsync(command.StoredFileId, cancellationToken);
        if (storedFile is null)
        {
            LogFileNotFound(logger, command.StoredFileId.Value);
            return;
        }

        Stream fileStream = await storageProvider.DownloadAsync(storedFile.StorageKey, cancellationToken);
        await using (fileStream)
        {
            FileScanResult scanResult = await fileScanner.ScanAsync(fileStream, storedFile.FileName, cancellationToken);

            if (scanResult.IsClean)
            {
                storedFile.MarkAsAvailable();
                LogFileScanPassed(logger, storedFile.Id.Value);
            }
            else
            {
                storedFile.MarkAsRejected();
                LogFileScanFailed(logger, storedFile.Id.Value, scanResult.ThreatName ?? "Unknown");
            }
        }

        await fileRepository.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stored file {FileId} not found for scan")]
    private static partial void LogFileNotFound(ILogger logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "File {FileId} passed security scan")]
    private static partial void LogFileScanPassed(ILogger logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File {FileId} failed security scan: {ThreatName}")]
    private static partial void LogFileScanFailed(ILogger logger, Guid fileId, string threatName);
}
