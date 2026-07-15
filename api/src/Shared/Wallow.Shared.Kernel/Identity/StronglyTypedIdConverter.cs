using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wallow.Shared.Kernel.Identity;

/// <summary>
/// Generic EF Core value converter for strongly-typed IDs.
/// Converts between the strongly-typed ID and its underlying Guid.
/// </summary>
public class StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId<TId>
{
    private static TId ConvertFromGuid(Guid guid) => TId.Create(guid);

    public StronglyTypedIdConverter()
        : base(
            id => id.Value,
            guid => ConvertFromGuid(guid))
    {
    }
}
