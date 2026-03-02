using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Storage.Infrastructure.Persistence.Repositories;

public sealed class StoredFileRepository : IStoredFileRepository
{
    private readonly StorageDbContext _context;

    public StoredFileRepository(StorageDbContext context)
    {
        _context = context;
    }

    public Task<StoredFile?> GetByIdAsync(StoredFileId id, CancellationToken cancellationToken = default)
    {
        return _context.Files
            .AsTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> GetByBucketIdAsync(
        StorageBucketId bucketId,
        string? pathPrefix = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StoredFile> query = _context.Files
            .Where(f => f.BucketId == bucketId);

        if (!string.IsNullOrWhiteSpace(pathPrefix))
        {
            query = query.Where(f => f.Path != null && f.Path.StartsWith(pathPrefix));
        }

        return await query
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> GetByTenantIdAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Files
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(StoredFile file)
    {
        _context.Files.Add(file);
    }

    public void Remove(StoredFile file)
    {
        _context.Files.Remove(file);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
