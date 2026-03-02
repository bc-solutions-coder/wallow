using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Storage.Infrastructure.Persistence.Repositories;

public sealed class StorageBucketRepository : IStorageBucketRepository
{
    private readonly StorageDbContext _context;

    public StorageBucketRepository(StorageDbContext context)
    {
        _context = context;
    }

    public Task<StorageBucket?> GetByIdAsync(StorageBucketId id, CancellationToken cancellationToken = default)
    {
        return _context.Buckets
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public Task<StorageBucket?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _context.Buckets
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<StorageBucket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Buckets
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _context.Buckets
            .AnyAsync(b => b.Name == name, cancellationToken);
    }

    public void Add(StorageBucket bucket)
    {
        _context.Buckets.Add(bucket);
    }

    public void Update(StorageBucket bucket)
    {
        _context.Buckets.Update(bucket);
    }

    public void Remove(StorageBucket bucket)
    {
        _context.Buckets.Remove(bucket);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
