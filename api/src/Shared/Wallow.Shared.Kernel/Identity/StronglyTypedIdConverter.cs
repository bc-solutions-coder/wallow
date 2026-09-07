using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wallow.Shared.Kernel.Identity;

/// <summary>
/// Converts strongly typed IDs to their Guid values for EF Core.
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
