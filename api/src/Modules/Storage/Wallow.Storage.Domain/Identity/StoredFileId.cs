using Wallow.Shared.Kernel.Identity;

namespace Wallow.Storage.Domain.Identity;

public readonly record struct StoredFileId(Guid Value) : IStronglyTypedId<StoredFileId>
{
    public static StoredFileId Create(Guid value) => new(value);
    public static StoredFileId New() => new(Guid.NewGuid());
}
