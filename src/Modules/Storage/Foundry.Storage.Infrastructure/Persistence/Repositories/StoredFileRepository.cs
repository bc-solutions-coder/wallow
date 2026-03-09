using Foundry.Shared.Kernel.Pagination;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Storage.Infrastructure.Persistence.Repositories;

public sealed class StoredFileRepository(StorageDbContext context) : IStoredFileRepository
{
    private static readonly Func<StorageDbContext, StoredFileId, CancellationToken, Task<StoredFile?>> _getByIdQuery =
        EF.CompileAsyncQuery(
            (StorageDbContext ctx, StoredFileId id, CancellationToken _) =>
                ctx.Files.AsTracking().FirstOrDefault(f => f.Id == id));

    public Task<StoredFile?> GetByIdAsync(StoredFileId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(context, id, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> GetByBucketIdAsync(
        StorageBucketId bucketId,
        string? pathPrefix = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StoredFile> query = context.Files
            .Where(f => f.BucketId == bucketId);

        if (!string.IsNullOrWhiteSpace(pathPrefix))
        {
            query = query.Where(f => f.Path != null && f.Path.StartsWith(pathPrefix));
        }

        return await query
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StoredFile>> GetByBucketIdPagedAsync(
        StorageBucketId bucketId,
        Guid tenantId,
        string? pathPrefix,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StoredFile> query = context.Files
            .Where(f => f.BucketId == bucketId && f.TenantId.Value == tenantId);

        if (!string.IsNullOrWhiteSpace(pathPrefix))
        {
            query = query.Where(f => f.Path != null && f.Path.StartsWith(pathPrefix));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<StoredFile> items = await query
            .OrderByDescending(f => f.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StoredFile>(items, totalCount, page, pageSize);
    }

    public void Add(StoredFile file)
    {
        context.Files.Add(file);
    }

    public void Remove(StoredFile file)
    {
        context.Files.Remove(file);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
