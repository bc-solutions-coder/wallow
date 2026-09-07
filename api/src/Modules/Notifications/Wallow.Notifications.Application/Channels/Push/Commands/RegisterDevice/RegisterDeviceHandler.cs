using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;

public sealed class RegisterDeviceHandler(
    IDeviceRegistrationRepository deviceRegistrationRepository,
    TimeProvider timeProvider,
    IWebPushConfiguration webPushConfiguration)
{
    public async Task<Result> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        DeviceRegistration registration;
        if (command.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush)
        {
            IReadOnlyList<DeviceRegistration> existingDevices = await deviceRegistrationRepository.GetActiveByUserAsync(command.UserId, cancellationToken);
            bool existingSubscription = existingDevices.Any(device => device.Subscription == command.Subscription
                && device.SigningKeyId == command.SigningKeyId);
            if (command.Subscription is null || command.SigningKeyId is null
                || !await webPushConfiguration.CanRegisterAsync(command.Subscription, command.SigningKeyId, existingSubscription, cancellationToken))
            {
                return Result.Failure(NotificationsErrors.WebPushInvalidSubscription);
            }
            registration = DeviceRegistration.RegisterWebPush(command.UserId, command.TenantId,
                command.Subscription, command.SigningKeyId, timeProvider.GetUtcNow());
        }
        else
        {
            registration = DeviceRegistration.Register(command.UserId, command.TenantId,
                command.Platform, command.Token!, timeProvider.GetUtcNow());
        }

        bool registered = await deviceRegistrationRepository.RegisterAsync(registration, cancellationToken);
        return registered ? Result.Success() : Result.Failure(NotificationsErrors.DeviceRegistrationConflict);
    }
}
