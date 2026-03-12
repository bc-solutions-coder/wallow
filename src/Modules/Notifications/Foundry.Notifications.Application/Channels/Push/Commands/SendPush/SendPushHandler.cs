using Foundry.Notifications.Application.Channels.Push.Commands.DeliverPush;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Shared.Kernel.Results;
using Wolverine;

namespace Foundry.Notifications.Application.Channels.Push.Commands.SendPush;

public sealed class SendPushHandler(
    INotificationPreferenceChecker preferenceChecker,
    IPushMessageRepository pushMessageRepository,
    IDeviceRegistrationRepository deviceRegistrationRepository,
    IMessageBus messageBus,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        SendPushCommand command,
        CancellationToken cancellationToken)
    {
        bool isEnabled = await preferenceChecker.IsChannelEnabledAsync(
            command.RecipientId,
            ChannelType.Push,
            command.NotificationType,
            cancellationToken);

        if (!isEnabled)
        {
            return Result.Success();
        }

        PushMessage pushMessage = PushMessage.Create(
            command.TenantId,
            command.RecipientId,
            command.Title,
            command.Body,
            timeProvider);

        pushMessageRepository.Add(pushMessage);
        await pushMessageRepository.SaveChangesAsync(cancellationToken);

        IReadOnlyList<DeviceRegistration> devices = await deviceRegistrationRepository
            .GetActiveByUserAsync(command.RecipientId, cancellationToken);

        foreach (DeviceRegistration device in devices)
        {
            DeliverPushCommand deliverCommand = new(
                pushMessage.Id,
                device.Id,
                device.Token,
                device.Platform);

            await messageBus.PublishAsync(deliverCommand);
        }

        return Result.Success();
    }
}
