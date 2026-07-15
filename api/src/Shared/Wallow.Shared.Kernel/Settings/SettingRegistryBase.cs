using System.Reflection;

namespace Wallow.Shared.Kernel.Settings;

public abstract class SettingRegistryBase : ISettingRegistry
{
    public abstract string ModuleName { get; }

    private readonly Lazy<Dictionary<string, object>> _defaults;
    private readonly Lazy<Dictionary<string, SettingMetadata>> _metadata;

    protected SettingRegistryBase()
    {
        _defaults = new Lazy<Dictionary<string, object>>(BuildDefaults);
        _metadata = new Lazy<Dictionary<string, SettingMetadata>>(BuildMetadata);
    }

    public IReadOnlyDictionary<string, object> Defaults => _defaults.Value;
    public IReadOnlyDictionary<string, SettingMetadata> Metadata => _metadata.Value;

    public bool IsCodeDefinedKey(string key) => _defaults.Value.ContainsKey(key);

    private Dictionary<string, object> BuildDefaults()
    {
        Dictionary<string, object> defaults = new(StringComparer.Ordinal);

        foreach (FieldInfo field in GetSettingDefinitionFields())
        {
            object? value = field.GetValue(null);
            if (value is null)
            {
                continue;
            }

            Type definitionType = field.FieldType;
            string key = (string)definitionType.GetProperty(nameof(SettingDefinition<object>.Key))!.GetValue(value)!;
            object defaultValue = definitionType.GetProperty(nameof(SettingDefinition<object>.DefaultValue))!.GetValue(value)!;

            defaults[key] = defaultValue;
        }

        return defaults;
    }

    private Dictionary<string, SettingMetadata> BuildMetadata()
    {
        Dictionary<string, SettingMetadata> metadata = new(StringComparer.Ordinal);

        foreach (FieldInfo field in GetSettingDefinitionFields())
        {
            object? value = field.GetValue(null);
            if (value is null)
            {
                continue;
            }

            Type definitionType = field.FieldType;
            string key = (string)definitionType.GetProperty(nameof(SettingDefinition<object>.Key))!.GetValue(value)!;
            object defaultValue = definitionType.GetProperty(nameof(SettingDefinition<object>.DefaultValue))!.GetValue(value)!;
            string description = (string)definitionType.GetProperty(nameof(SettingDefinition<object>.Description))!.GetValue(value)!;
            Type valueType = (Type)definitionType.GetProperty(nameof(SettingDefinition<object>.ValueType))!.GetValue(value)!;

            metadata[key] = new SettingMetadata(
                Key: key,
                DisplayName: field.Name,
                Description: description,
                ValueType: valueType,
                DefaultValue: defaultValue);
        }

        return metadata;
    }

    private FieldInfo[] GetSettingDefinitionFields()
    {
        return GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsInitOnly
                        && f.FieldType.IsGenericType
                        && f.FieldType.GetGenericTypeDefinition() == typeof(SettingDefinition<>))
            .ToArray();
    }
}
