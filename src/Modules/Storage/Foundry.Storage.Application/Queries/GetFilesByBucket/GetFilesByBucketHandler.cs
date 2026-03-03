using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Mappings;
using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Application.Queries.GetFilesByBucket;

public sealed class GetFilesByBucketHandler(
    IStorageBucketRepository bucketRepository,
    IStoredFileRepository fileRepository)
{
    public async Task<Result<PagedResult<StoredFileDto>>> Handle(
        GetFilesByBucketQuery query,
        CancellationToken cancellationToken)
    {
        StorageBucket? bucket = await bucketRepository.GetByNameAsync(query.BucketName, cancellationToken);
        if (bucket is null)
        {
            return Result.Failure<PagedResult<StoredFileDto>>(
                Error.NotFound("Bucket", query.BucketName));
        }

        PagedResult<StoredFile> pagedFiles = await fileRepository.GetByBucketIdPagedAsync(
            bucket.Id,
            query.TenantId,
            query.PathPrefix,
            query.Page,
            query.PageSize,
            cancellationToken);

        List<StoredFileDto> dtos = pagedFiles.Items
            .Select(f => f.ToDto())
            .ToList();

        PagedResult<StoredFileDto> pagedResult = new(
            dtos,
            pagedFiles.TotalCount,
            query.Page,
            query.PageSize);

        return Result.Success(pagedResult);
    }
}
