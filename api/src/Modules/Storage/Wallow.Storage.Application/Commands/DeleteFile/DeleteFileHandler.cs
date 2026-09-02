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

        // Commit the row removal BEFORE deleting the object, never after. An object-store delete
        // is not undone by a database rollback, so the reverse order lets a failed commit leave a
        // row pointing at bytes that are already gone -- a permanent 404 on read. This way the
        // worst case is an orphaned object: garbage, but nothing points at it.
        string storageKey = file.StorageKey;

        file.MarkAsDeleted();
        fileRepository.Remove(file);
        await fileRepository.SaveChangesAsync(cancellationToken);

        await storageProvider.DeleteAsync(storageKey, cancellationToken);

        return Result.Success();
    }
}
