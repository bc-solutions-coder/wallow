using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct ServiceAccountMetadataId(Guid Value) : IStronglyTypedId<ServiceAccountMetadataId>
{
    public static ServiceAccountMetadataId Create(Guid value) => new(value);
    public static ServiceAccountMetadataId New() => new(Guid.NewGuid());
}
