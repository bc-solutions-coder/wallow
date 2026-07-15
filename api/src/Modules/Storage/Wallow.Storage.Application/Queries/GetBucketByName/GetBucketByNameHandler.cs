using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Mappings;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Application.Queries.GetBucketByName;

public sealed class GetBucketByNameHandler(IStorageBucketRepository bucketRepository)
{
    public async Task<Result<BucketDto>> Handle(
        GetBucketByNameQuery query,
        CancellationToken cancellationToken)
    {
        StorageBucket? bucket = await bucketRepository.GetByNameAsync(query.Name, cancellationToken);

        if (bucket is null)
        {
            return Result.Failure<BucketDto>(Error.NotFound("Bucket", query.Name));
        }

        return Result.Success(bucket.ToDto());
    }
}
