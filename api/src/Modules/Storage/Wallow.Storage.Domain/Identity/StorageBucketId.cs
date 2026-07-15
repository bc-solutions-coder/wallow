using Wallow.Shared.Kernel.Identity;

namespace Wallow.Storage.Domain.Identity;

public readonly record struct StorageBucketId(Guid Value) : IStronglyTypedId<StorageBucketId>
{
    public static StorageBucketId Create(Guid value) => new(value);
    public static StorageBucketId New() => new(Guid.NewGuid());
}
