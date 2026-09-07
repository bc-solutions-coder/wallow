using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Wallow.ServiceDefaults;

internal static partial class TelemetryPrivacy
{
    private const int AttributeLimit = 32;
    private const int ValueBytes = 1024;
    private const int DepthLimit = 5;
    private const int NodeLimit = 256;

    public static bool IsLabel(string value) => Label().IsMatch(value);

    public static string Name(string? value, string fallback) => value is not null && Identifier().IsMatch(value) ? value : fallback;

    public static Dictionary<string, object?> Attributes(IEnumerable<KeyValuePair<string, object?>> values)
    {
        int remaining = NodeLimit;
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach ((string key, object? value) in values.Take(AttributeLimit))
        {
            result[Name(key, "attribute")] = Sensitive().IsMatch(key) ? "[redacted]" : Value(value, 0, ref remaining);
        }
        return result;
    }

    private static object? Value(object? value, int depth, ref int remaining)
    {
        if (--remaining < 0 || depth > DepthLimit)
        {
            return "[limit]";
        }

        if (value is null or bool or int or long or uint or ulong or short or byte or decimal)
        {
            return value;
        }

        if (value is double number)
        {
            return double.IsFinite(number) ? number : null;
        }

        if (value is float single)
        {
            return float.IsFinite(single) ? single : null;
        }

        if (value is string text)
        {
            if (text.Length > ValueBytes)
            {
                return "[oversize]";
            }

            text = EmbeddedUrl().Replace(text, match => Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri)
                ? uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped) : "[redacted]");
            if (text.StartsWith('/'))
            {
                text = text.Split('?', '#')[0];
            }

            if (SensitiveValue().IsMatch(Uri.UnescapeDataString(text)))
            {
                return "[redacted]";
            }

            return Encoding.UTF8.GetByteCount(text) <= ValueBytes ? text : "[oversize]";
        }
        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            Dictionary<string, object?> result = new(StringComparer.Ordinal);
            foreach ((string key, object? item) in dictionary.Take(AttributeLimit))
            {
                result[Name(key, "attribute")] = Sensitive().IsMatch(key) ? "[redacted]" : Value(item, depth + 1, ref remaining);
            }
            return result;
        }
        if (value is IEnumerable sequence)
        {
            List<object?> result = [];
            foreach (object? item in sequence)
            {
                if (result.Count == AttributeLimit)
                {
                    break;
                }

                result.Add(Value(item, depth + 1, ref remaining));
            }
            return result;
        }
        return "[redacted]";
    }

    public static object[] WireAttributes(IEnumerable<KeyValuePair<string, object?>> values) => Attributes(values)
        .Select(pair => (object)new { key = pair.Key, value = new { stringValue = pair.Value is string text ? text : System.Text.Json.JsonSerializer.Serialize(pair.Value) } }).ToArray();

    public static string Stack(Exception error) => string.Join('\n', new StackTrace(error, true).GetFrames().Take(6)
        .Select(frame => $"{Name(Path.GetFileName(frame.GetFileName()), "unknown")}:{frame.GetFileLineNumber()}:{frame.GetFileColumnNumber()}"));

    [GeneratedRegex("https?://[^\\s\"<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmbeddedUrl();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Label();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Identifier();

    [GeneratedRegex("name|email|address|ip$|body|form|cookie|token|secret|pass|auth|baggage|session|user|org|service|application|registration|tenant", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Sensitive();

    [GeneratedRegex(@"[\w.+-]+@[\w.-]+|\b(?:\d{1,3}\.){3}\d{1,3}\b|\b(?:[0-9a-f]{0,4}:){2,}[0-9a-f:]+\b|::[0-9a-f:]+|\b(?:Bearer|Basic)\s+\S+|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SensitiveValue();
}
