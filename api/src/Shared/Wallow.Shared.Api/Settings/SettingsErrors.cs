using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Settings;

/// <summary>
/// Shared error codes for module settings endpoints.
/// </summary>
public static class SettingsErrors
{
    public static readonly ErrorCatalogEntry SystemKeyBlocked = new(
        "Settings.SystemKeyBlocked", ErrorKind.BusinessRule, "System keys cannot be modified through this endpoint");

    public static readonly ErrorCatalogEntry UnknownKey = new(
        "Settings.UnknownKey", ErrorKind.Validation, "Unknown setting key");
}
