namespace Wallow.Shared.Kernel.Settings;

public interface ISettingRegistry
{
    string ModuleName { get; }
    IReadOnlyDictionary<string, object> Defaults { get; }
    IReadOnlyDictionary<string, SettingMetadata> Metadata { get; }
    bool IsCodeDefinedKey(string key);
}

public sealed record SettingMetadata(
    string Key,
    string DisplayName,
    string Description,
    Type ValueType,
    object DefaultValue);
