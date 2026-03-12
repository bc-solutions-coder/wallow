using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Preferences.Commands;

public sealed record SetChannelPreferenceCommand(
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled);

public sealed class SetChannelPreferenceHandler(
    IChannelPreferenceRepository preferenceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<ChannelPreferenceDto>> Handle(
        SetChannelPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        ChannelPreference? preference = await preferenceRepository.GetByUserChannelAndNotificationTypeAsync(
            command.UserId,
            command.ChannelType,
            command.NotificationType,
            cancellationToken);

        if (preference is null)
        {
            preference = ChannelPreference.Create(
                command.UserId,
                command.ChannelType,
                command.NotificationType,
                timeProvider,
                command.IsEnabled);

            preferenceRepository.Add(preference);
        }
        else
        {
            if (command.IsEnabled)
            {
                preference.Enable(timeProvider);
            }
            else
            {
                preference.Disable(timeProvider);
            }
        }

        await preferenceRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new ChannelPreferenceDto(
            preference.Id.Value,
            preference.UserId,
            preference.ChannelType,
            preference.NotificationType,
            preference.IsEnabled,
            preference.CreatedAt,
            preference.UpdatedAt));
    }
}
