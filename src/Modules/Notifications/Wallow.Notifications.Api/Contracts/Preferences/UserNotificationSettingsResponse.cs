using Wallow.Notifications.Domain.Preferences;

namespace Wallow.Notifications.Api.Contracts.Preferences;

public sealed record UserNotificationSettingsResponse(
    Guid UserId,
    IReadOnlyList<ChannelSettingResponse> ChannelSettings);

public sealed record ChannelSettingResponse(
    ChannelType ChannelType,
    bool IsGloballyEnabled,
    IReadOnlyList<ChannelPreferenceResponse> TypePreferences);

public sealed record ChannelPreferenceResponse(
    Guid Id,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
