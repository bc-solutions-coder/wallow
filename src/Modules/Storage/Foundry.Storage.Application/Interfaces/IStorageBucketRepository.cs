using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Application.Interfaces;

public interface IStorageBucketRepository
{
    Task<StorageBucket?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    void Add(StorageBucket bucket);
    void Remove(StorageBucket bucket);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
