using Wallow.Notifications.Application.Channels.Email.DTOs;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.Channels.Email.Mappings;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;

public sealed class UpdateEmailPreferencesHandler(
    IEmailPreferenceRepository preferenceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<EmailPreferenceDto>> Handle(
        UpdateEmailPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        EmailPreference? preference = await preferenceRepository.GetByUserAndTypeAsync(
            command.UserId,
            command.NotificationType,
            cancellationToken);

        if (preference is null)
        {
            preference = EmailPreference.Create(
                command.UserId,
                command.NotificationType,
                command.IsEnabled,
                timeProvider);

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

        return Result.Success(preference.ToDto());
    }
}
