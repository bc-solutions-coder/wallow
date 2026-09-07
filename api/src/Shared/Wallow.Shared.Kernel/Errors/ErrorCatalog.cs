using System.Reflection;

namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// Aggregates shared and registered catalogs by reflecting their public static
/// <see cref="ErrorCatalogEntry"/> fields and properties.
/// </summary>
/// <remarks>
/// The OpenAPI document exports these entries as the <c>ErrorCode</c> enum.
/// </remarks>
public sealed class ErrorCatalog
{
    private ErrorCatalog(IReadOnlyList<ErrorCatalogEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>Gets every entry, ordered by code.</summary>
    public IReadOnlyList<ErrorCatalogEntry> Entries { get; }

    /// <summary>
    /// Builds the aggregate of <see cref="SharedErrors"/> and the given catalog types. A type
    /// listed twice counts once; duplicate codes are a configuration error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two catalogs declare the same code.</exception>
    public static ErrorCatalog Aggregate(IEnumerable<Type> catalogTypes)
    {
        ArgumentNullException.ThrowIfNull(catalogTypes);

        Dictionary<string, (ErrorCatalogEntry Entry, Type Owner)> byCode = new(StringComparer.Ordinal);

        foreach (Type catalogType in catalogTypes.Prepend(typeof(SharedErrors)).Distinct())
        {
            foreach (ErrorCatalogEntry entry in EntriesOf(catalogType))
            {
                if (byCode.TryGetValue(entry.Code, out (ErrorCatalogEntry Entry, Type Owner) existing))
                {
                    throw new InvalidOperationException(
                        $"Error code '{entry.Code}' is declared by both {existing.Owner.FullName} and {catalogType.FullName}; every code has exactly one owner.");
                }

                byCode[entry.Code] = (entry, catalogType);
            }
        }

        return new ErrorCatalog(byCode.Values
            .Select(value => value.Entry)
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>
    /// Reads the entries a catalog type declares: its public static fields and properties of
    /// type <see cref="ErrorCatalogEntry"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The type declares no entries.</exception>
    public static IReadOnlyList<ErrorCatalogEntry> EntriesOf(Type catalogType)
    {
        ArgumentNullException.ThrowIfNull(catalogType);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        IEnumerable<ErrorCatalogEntry?> fromFields = catalogType
            .GetFields(flags)
            .Where(field => field.FieldType == typeof(ErrorCatalogEntry))
            .Select(field => (ErrorCatalogEntry?)field.GetValue(null));

        IEnumerable<ErrorCatalogEntry?> fromProperties = catalogType
            .GetProperties(flags)
            .Where(property => property.PropertyType == typeof(ErrorCatalogEntry) && property.GetIndexParameters().Length == 0)
            .Select(property => (ErrorCatalogEntry?)property.GetValue(null));

        List<ErrorCatalogEntry> entries = fromFields
            .Concat(fromProperties)
            .OfType<ErrorCatalogEntry>()
            .ToList();

        if (entries.Count == 0)
        {
            throw new ArgumentException(
                $"{catalogType.FullName} declares no public static {nameof(ErrorCatalogEntry)} members, so it is not an error catalog.",
                nameof(catalogType));
        }

        return entries;
    }
}
