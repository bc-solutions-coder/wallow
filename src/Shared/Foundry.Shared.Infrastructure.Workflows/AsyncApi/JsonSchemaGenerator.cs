using System.Reflection;
using System.Text.Json.Nodes;

namespace Foundry.Shared.Infrastructure.Workflows.AsyncApi;

/// <summary>
/// Converts C# types to JSON Schema objects for AsyncAPI message payloads.
/// Property names are camelCased to match JSON serialization conventions.
/// </summary>
public static class JsonSchemaGenerator
{
    public static JsonObject GenerateSchema(Type type)
    {
        JsonObject properties = new();
        JsonArray required = new();

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Concat(GetBaseProperties(type)))
        {
            string camelName = ToCamelCase(prop.Name);
            properties[camelName] = GetPropertySchema(prop.PropertyType);

            if (!IsNullable(prop))
            {
                required.Add(camelName);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    public static JsonObject GetPropertySchema(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return SimpleType("string");
        }

        if (underlying == typeof(bool))
        {
            return SimpleType("boolean");
        }

        if (underlying == typeof(int))
        {
            return SimpleType("integer");
        }

        if (underlying == typeof(long))
        {
            return SimpleType("integer", "int64");
        }

        if (underlying == typeof(decimal))
        {
            return SimpleType("number");
        }

        if (underlying == typeof(double))
        {
            return SimpleType("number", "double");
        }

        if (underlying == typeof(float))
        {
            return SimpleType("number", "float");
        }

        if (underlying == typeof(Guid))
        {
            return SimpleType("string", "uuid");
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            return SimpleType("string", "date-time");
        }

        if (underlying == typeof(TimeSpan))
        {
            return SimpleType("string", "duration");
        }

        if (IsCollection(underlying))
        {
            Type elementType = GetCollectionElementType(underlying);
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = GetPropertySchema(elementType)
            };
        }

        if (underlying.IsClass || underlying.IsValueType && !underlying.IsPrimitive)
        {
            return GenerateSchema(underlying);
        }

        return SimpleType("string");
    }

    private static JsonObject SimpleType(string type, string? format = null)
    {
        JsonObject obj = new() { ["type"] = type };
        if (format is not null)
        {
            obj["format"] = format;
        }

        return obj;
    }

    private static IEnumerable<PropertyInfo> GetBaseProperties(Type type)
    {
        Type? baseType = type.BaseType;
        while (baseType is not null && baseType != typeof(object))
        {
            foreach (PropertyInfo prop in baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                yield return prop;
            }

            baseType = baseType.BaseType;
        }
    }

    private static bool IsNullable(PropertyInfo prop)
    {
        if (Nullable.GetUnderlyingType(prop.PropertyType) is not null)
        {
            return true;
        }

        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        return info.ReadState == NullabilityState.Nullable;
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) &&
        type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    private static Type GetCollectionElementType(Type type)
    {
        Type? enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0] ?? typeof(object);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
