using Foundry.Notifications.Application.Channels.Preferences.DTOs;
using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;

public sealed class GetUserNotificationSettingsHandler(IChannelPreferenceRepository preferenceRepository)
{
    public async Task<Result<UserNotificationSettingsDto>> Handle(
        GetUserNotificationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChannelPreference> preferences = await preferenceRepository.GetByUserIdAsync(
            query.UserId,
            cancellationToken);

        IEnumerable<IGrouping<ChannelType, ChannelPreference>> grouped = preferences
            .GroupBy(p => p.ChannelType);

        List<ChannelSettingDto> channelSettings = grouped.Select(group =>
        {
            ChannelPreference? globalPref = group.FirstOrDefault(p => p.NotificationType == "*");
            bool isGloballyEnabled = globalPref?.IsEnabled ?? true;

            List<ChannelPreferenceDto> typePreferences = group
                .Where(p => p.NotificationType != "*")
                .Select(p => new ChannelPreferenceDto(
                    p.Id.Value,
                    p.UserId,
                    p.ChannelType,
                    p.NotificationType,
                    p.IsEnabled,
                    p.CreatedAt,
                    p.UpdatedAt))
                .ToList();

            return new ChannelSettingDto(
                group.Key,
                isGloballyEnabled,
                typePreferences);
        }).ToList();

        UserNotificationSettingsDto dto = new(query.UserId, channelSettings);

        return Result.Success(dto);
    }
}
