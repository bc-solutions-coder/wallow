using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Wallow.Shared.Infrastructure.Core.Persistence;

/// <summary>
/// ValueComparer for Dictionary&lt;string, object&gt; properties stored as JSON.
/// Required when using value converters on collection types.
/// </summary>
public sealed class DictionaryValueComparer : ValueComparer<Dictionary<string, object>?>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DictionaryValueComparer() : base(
        (d1, d2) => AreEqual(d1, d2),
        d => GetDictionaryHashCode(d),
        d => CreateSnapshot(d))
    {
    }

    private static bool AreEqual(Dictionary<string, object>? left, Dictionary<string, object>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        // Compare by serializing to JSON - handles nested objects correctly
        string leftJson = JsonSerializer.Serialize(left, _jsonOptions);
        string rightJson = JsonSerializer.Serialize(right, _jsonOptions);
        return leftJson == rightJson;
    }

    private static int GetDictionaryHashCode(Dictionary<string, object>? dictionary)
    {
        if (dictionary is null)
        {
            return 0;
        }

        string json = JsonSerializer.Serialize(dictionary, _jsonOptions);
        return json.GetHashCode(StringComparison.Ordinal);
    }

    private static Dictionary<string, object>? CreateSnapshot(Dictionary<string, object>? source)
    {
        if (source is null)
        {
            return null;
        }
        // Deep clone via JSON serialization
        string json = JsonSerializer.Serialize(source, _jsonOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
    }
}
