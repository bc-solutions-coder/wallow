using Wallow.Notifications.Application.Channels.Push.Commands.DeliverPush;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Notifications.Application.Channels.Push.Commands.SendPush;

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
