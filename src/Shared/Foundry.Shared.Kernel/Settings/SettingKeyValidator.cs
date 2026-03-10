namespace Foundry.Shared.Kernel.Settings;

public enum SettingKeyValidationResult
{
    Valid,
    InvalidKey,
    CustomKeyLimitExceeded,
    SystemKeyUnauthorized,

    // Legacy values used by existing controllers
    CodeDefined,
    Custom,
    System,
    Unknown
}

public static class SettingKeyValidator
{
    public const string CustomPrefix = "custom.";
    public const string SystemPrefix = "system.";
    public const int MaxCustomKeysPerTenant = 100;

    public static bool IsCustomKey(string key) => key.StartsWith(CustomPrefix, StringComparison.Ordinal);
    public static bool IsSystemKey(string key) => key.StartsWith(SystemPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Full validation with authorization and custom key limit enforcement.
    /// </summary>
    public static SettingKeyValidationResult Validate(
        string key,
        ISettingRegistry registry,
        bool isPlatformAdmin,
        int currentCustomKeyCount)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return SettingKeyValidationResult.InvalidKey;
        }

        if (IsSystemKey(key))
        {
            return isPlatformAdmin
                ? SettingKeyValidationResult.Valid
                : SettingKeyValidationResult.SystemKeyUnauthorized;
        }

        if (IsCustomKey(key))
        {
            return currentCustomKeyCount >= MaxCustomKeysPerTenant
                ? SettingKeyValidationResult.CustomKeyLimitExceeded
                : SettingKeyValidationResult.Valid;
        }

        return registry.IsCodeDefinedKey(key)
            ? SettingKeyValidationResult.Valid
            : SettingKeyValidationResult.InvalidKey;
    }

    /// <summary>
    /// Classifies a key by namespace without enforcing authorization or limits.
    /// </summary>
    public static SettingKeyValidationResult Validate(string key, ISettingRegistry registry)
    {
        if (IsCustomKey(key))
        {
            return SettingKeyValidationResult.Custom;
        }

        if (IsSystemKey(key))
        {
            return SettingKeyValidationResult.System;
        }

        if (registry.IsCodeDefinedKey(key))
        {
            return SettingKeyValidationResult.CodeDefined;
        }

        return SettingKeyValidationResult.Unknown;
    }
}
