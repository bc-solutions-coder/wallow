using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Settings;

/// <summary>
/// Codes for the setting-key checks every module's settings endpoints share. The condition is one
/// whichever module's endpoint reaches it, so the shared API surface owns the code rather than
/// each module minting its own.
/// </summary>
public static class SettingsErrors
{
    public static readonly ErrorCatalogEntry SystemKeyBlocked = new(
        "Settings.SystemKeyBlocked", ErrorKind.BusinessRule, "System keys cannot be modified through this endpoint");

    public static readonly ErrorCatalogEntry UnknownKey = new(
        "Settings.UnknownKey", ErrorKind.Validation, "Unknown setting key");
}
