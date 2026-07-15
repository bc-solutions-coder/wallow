namespace Wallow.Shared.Kernel.Settings;

public sealed record SettingDefinition<T>(
    string Key,
    T DefaultValue,
    string Description)
{
    public Type ValueType => typeof(T);
}
