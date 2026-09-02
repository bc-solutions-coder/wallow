using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Api.Settings;

/// <summary>
/// Turns a <see cref="SettingKeyValidator"/> classification into the <see cref="Result"/> a
/// settings endpoint answers with, so every module refuses a key with the same code.
/// </summary>
public static class SettingKeyValidationExtensions
{
    /// <summary>
    /// Success for a key the endpoint may write; otherwise the shared failure for a system key or
    /// a key no registry defines.
    /// </summary>
    public static Result ToResult(this SettingKeyValidationResult validation, string key)
    {
        return validation switch
        {
            SettingKeyValidationResult.System => Result.Failure(SettingsErrors.SystemKeyBlocked),
            SettingKeyValidationResult.Unknown => Result.Failure(
                SettingsErrors.UnknownKey, $"Unknown setting key '{key}'"),
            _ => Result.Success()
        };
    }
}
