using Microsoft.EntityFrameworkCore;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Infrastructure.Persistence.Repositories;

public sealed class StorageBucketRepository(StorageDbContext context) : IStorageBucketRepository
{
    private static readonly Func<StorageDbContext, string, CancellationToken, Task<StorageBucket?>> _getByNameQuery =
        EF.CompileAsyncQuery(
            (StorageDbContext ctx, string name, CancellationToken _) =>
                ctx.Buckets.AsTracking().FirstOrDefault(b => b.Name == name));

    public Task<StorageBucket?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _getByNameQuery(context, name, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return context.Buckets
            .AnyAsync(b => b.Name == name, cancellationToken);
    }

    public void Add(StorageBucket bucket)
    {
        context.Buckets.Add(bucket);
    }

    public void Remove(StorageBucket bucket)
    {
        context.Buckets.Remove(bucket);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
