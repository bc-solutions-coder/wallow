using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Errors;

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
            return Result.Failure(StorageErrors.BucketNotFound);
        }

        if (bucket.TenantId.Value != command.TenantId)
        {
            return Result.Failure(StorageErrors.BucketNotFound);
        }

        IReadOnlyList<StoredFile> files = await fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: cancellationToken);

        if (files.Count > 0 && !command.Force)
        {
            return Result.Failure(
                new Error(StorageErrors.BucketNotEmpty, $"Bucket '{command.Name}' contains {files.Count} file(s). Use force=true to delete anyway."));
        }

        // Commit every removal BEFORE deleting the objects, never after -- the same rule
        // DeleteFileHandler follows, and sharper here because the loop can strand a whole bucket's
        // worth of rows. An object-store delete is not undone by a database rollback, so deleting
        // inside the loop would let a failed commit leave rows describing files that are gone, and
        // a bucket row describing a bucket that no longer exists. Collect the keys, commit, then
        // delete; the worst case becomes orphaned objects rather than unreadable rows.
        List<string> storageKeys = [];

        if (command.Force && files.Count > 0)
        {
            foreach (StoredFile file in files)
            {
                storageKeys.Add(file.StorageKey);
                file.MarkAsDeleted();
                fileRepository.Remove(file);
            }
        }

        bucket.Delete();
        bucketRepository.Remove(bucket);
        await bucketRepository.SaveChangesAsync(cancellationToken);

        foreach (string storageKey in storageKeys)
        {
            await storageProvider.DeleteAsync(storageKey, cancellationToken);
        }

        return Result.Success();
    }
}
