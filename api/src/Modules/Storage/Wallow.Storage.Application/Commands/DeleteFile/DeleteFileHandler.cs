using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Errors;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Application.Commands.DeleteFile;

public sealed class DeleteFileHandler(
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider)
{
    public async Task<Result> Handle(
        DeleteFileCommand command,
        CancellationToken cancellationToken)
    {
        StoredFileId fileId = StoredFileId.Create(command.FileId);
        StoredFile? file = await fileRepository.GetByIdAsync(fileId, cancellationToken);

        if (file is null)
        {
            return Result.Failure(StorageErrors.FileNotFound);
        }

        // Commit row removals before deleting objects: a database rollback cannot restore bytes.
        // A failed object delete then leaves an orphan rather than a row pointing at missing bytes.
        string storageKey = file.StorageKey;

        file.MarkAsDeleted();
        fileRepository.Remove(file);
        await fileRepository.SaveChangesAsync(cancellationToken);

        await storageProvider.DeleteAsync(storageKey, cancellationToken);

        return Result.Success();
    }
}
