using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Application.Commands.DeleteBucket;

public sealed class DeleteBucketHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider)
{
    public async Task<Result> Handle(
        DeleteBucketCommand command,
        CancellationToken cancellationToken)
    {
        StorageBucket? bucket = await bucketRepository.GetByNameAsync(command.Name, cancellationToken);
        if (bucket is null)
        {
            return Result.Failure(Error.NotFound("Bucket", command.Name));
        }

        if (bucket.TenantId.Value != command.TenantId)
        {
            return Result.Failure(Error.NotFound("Bucket", command.Name));
        }

        IReadOnlyList<StoredFile> files = await fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: cancellationToken);

        if (files.Count > 0 && !command.Force)
        {
            return Result.Failure(
                Error.Validation($"Bucket '{command.Name}' contains {files.Count} file(s). Use force=true to delete anyway."));
        }

        if (command.Force && files.Count > 0)
        {
            foreach (StoredFile file in files)
            {
                await storageProvider.DeleteAsync(file.StorageKey, cancellationToken);
                file.MarkAsDeleted();
                fileRepository.Remove(file);
            }
        }

        bucket.Delete();
        bucketRepository.Remove(bucket);
        await bucketRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
