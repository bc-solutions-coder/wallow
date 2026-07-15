using Wallow.Shared.Kernel.Pagination;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Application.Interfaces;

public interface IStoredFileRepository
{
    Task<StoredFile?> GetByIdAsync(StoredFileId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredFile>> GetByBucketIdAsync(StorageBucketId bucketId, string? pathPrefix = null, CancellationToken cancellationToken = default);
    Task<PagedResult<StoredFile>> GetByBucketIdPagedAsync(StorageBucketId bucketId, string? pathPrefix, int page, int pageSize, CancellationToken cancellationToken = default);
    void Add(StoredFile file);
    void Remove(StoredFile file);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
