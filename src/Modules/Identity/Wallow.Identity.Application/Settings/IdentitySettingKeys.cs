using Wallow.Shared.Kernel.Settings;

namespace Wallow.Identity.Application.Settings;

public class IdentitySettingKeys : SettingRegistryBase
{
    public override string ModuleName => "identity";

    public static readonly SettingDefinition<string> Timezone = new(
        Key: "identity.timezone",
        DefaultValue: "UTC",
        Description: "The default timezone for the tenant");

    public static readonly SettingDefinition<string> Locale = new(
        Key: "identity.locale",
        DefaultValue: "en-US",
        Description: "The default locale for the tenant");

    public static readonly SettingDefinition<string> DateFormat = new(
        Key: "identity.date_format",
        DefaultValue: "YYYY-MM-DD",
        Description: "Date format used in identity-related displays and exports");

    public static readonly SettingDefinition<string> Theme = new(
        Key: "identity.theme",
        DefaultValue: "light",
        Description: "The default UI theme for the tenant");
}
