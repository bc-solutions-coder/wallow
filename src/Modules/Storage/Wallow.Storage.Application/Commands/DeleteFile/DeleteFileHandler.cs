using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
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
            return Result.Failure(Error.NotFound("File", command.FileId));
        }

        if (file.TenantId.Value != command.TenantId)
        {
            return Result.Failure(Error.NotFound("File", command.FileId));
        }

        await storageProvider.DeleteAsync(file.StorageKey, cancellationToken);

        fileRepository.Remove(file);
        await fileRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
