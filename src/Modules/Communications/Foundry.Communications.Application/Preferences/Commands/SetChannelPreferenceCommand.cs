using Foundry.Communications.Application.Preferences.DTOs;
using Foundry.Communications.Application.Preferences.Interfaces;
using Foundry.Communications.Domain.Preferences;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Preferences.Commands;

public sealed record SetChannelPreferenceCommand(
    Guid TenantId,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled);

public sealed class SetChannelPreferenceHandler(IChannelPreferenceRepository preferenceRepository)
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
                command.IsEnabled);

            preferenceRepository.Add(preference);
        }
        else
        {
            if (command.IsEnabled)
            {
                preference.Enable();
            }
            else
            {
                preference.Disable();
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
