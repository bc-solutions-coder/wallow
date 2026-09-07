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
    TimeProvider timeProvider,
    IWebPushConfiguration webPushConfiguration)
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

        IReadOnlyList<DeviceRegistration> devices = await deviceRegistrationRepository
            .GetActiveByUserAsync(command.RecipientId, cancellationToken);
        List<DeviceRegistration> eligibleDevices = [];
        foreach (DeviceRegistration device in devices)
        {
            if (device.Platform != Domain.Channels.Push.Enums.PushPlatform.WebPush
                || await webPushConfiguration.IsAvailableAsync(device.SigningKeyId, cancellationToken))
            {
                eligibleDevices.Add(device);
            }
        }
        if (devices.Count > 0 && eligibleDevices.Count == 0)
        {
            return Result.Failure(Domain.Errors.NotificationsErrors.WebPushUnavailable);
        }

        PushMessage pushMessage = PushMessage.Create(
            command.TenantId,
            command.RecipientId,
            command.Title,
            command.Body,
            timeProvider);

        pushMessage.SetClickPath(command.ClickPath);
        pushMessageRepository.Add(pushMessage);
        await pushMessageRepository.SaveChangesAsync(cancellationToken);


        foreach (DeviceRegistration device in eligibleDevices)
        {
            DeliverPushCommand deliverCommand = new(
                pushMessage.Id,
                device.Id,
                command.TenantId.Value);

            await messageBus.PublishAsync(deliverCommand);
        }

        return Result.Success();
    }
}
