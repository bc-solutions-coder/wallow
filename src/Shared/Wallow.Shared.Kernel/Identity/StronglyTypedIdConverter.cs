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

/// <summary>
/// Extension methods for configuring strongly-typed IDs in EF Core.
/// </summary>
public static class StronglyTypedIdExtensions
{
    /// <summary>
    /// Creates a new ID if the value is empty, otherwise returns the existing value.
    /// Useful for ensuring entities always have a valid ID.
    /// </summary>
    public static TId EnsureId<TId>(this TId id)
        where TId : struct, IStronglyTypedId<TId>
    {
        return id.Value == Guid.Empty ? TId.New() : id;
    }
}
