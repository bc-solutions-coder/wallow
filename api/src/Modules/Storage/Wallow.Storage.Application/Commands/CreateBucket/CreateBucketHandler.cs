using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Mappings;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.ValueObjects;

namespace Wallow.Storage.Application.Commands.CreateBucket;

public sealed class CreateBucketHandler(
    IStorageBucketRepository bucketRepository,
    ITenantContext tenantContext)
{
    public async Task<Result<BucketDto>> Handle(
        CreateBucketCommand command,
        CancellationToken cancellationToken)
    {
        bool exists = await bucketRepository.ExistsByNameAsync(command.Name, cancellationToken);
        if (exists)
        {
            return Result.Failure<BucketDto>(
                Error.Conflict($"Bucket '{command.Name}' already exists"));
        }

        RetentionPolicy? retention = null;
        if (command.RetentionDays.HasValue && command.RetentionAction.HasValue)
        {
            retention = new RetentionPolicy(command.RetentionDays.Value, command.RetentionAction.Value);
        }

        StorageBucket bucket = StorageBucket.Create(
            tenantContext.TenantId,
            command.Name,
            command.Description,
            command.Access,
            command.MaxFileSizeBytes,
            command.AllowedContentTypes,
            retention,
            command.Versioning);

        bucketRepository.Add(bucket);
        await bucketRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(bucket.ToDto());
    }
}
