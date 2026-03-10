using Foundry.Shared.Kernel.Settings;

namespace Foundry.Communications.Application.Settings;

public class CommunicationsSettingKeys : SettingRegistryBase
{
    public override string ModuleName => "communications";

    public static readonly SettingDefinition<string> EmailSenderName = new(
        Key: "communications.email_sender_name",
        DefaultValue: "Foundry",
        Description: "The display name used as the sender for outgoing emails");

    public static readonly SettingDefinition<string> NotificationPreferences = new(
        Key: "communications.notification_preferences",
        DefaultValue: "email",
        Description: "Default notification channel preferences for users");
}
