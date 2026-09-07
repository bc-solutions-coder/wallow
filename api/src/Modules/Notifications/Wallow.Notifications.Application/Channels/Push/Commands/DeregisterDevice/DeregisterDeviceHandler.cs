using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

public sealed class DeregisterDeviceHandler(
    IDeviceRegistrationRepository deviceRegistrationRepository)
{
    public async Task<Result> Handle(
        DeregisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        DeviceRegistration? registration = await deviceRegistrationRepository.GetByIdAsync(
            command.DeviceRegistrationId, cancellationToken);

        if (registration is null || registration.UserId != command.UserId)
        {
            return Result.Failure(NotificationsErrors.DeviceRegistrationNotFound);
        }

        registration.Deactivate();
        bool removed = await deviceRegistrationRepository.SaveDeactivationAsync(registration, cancellationToken);
        return removed ? Result.Success() : Result.Failure(NotificationsErrors.DeviceRegistrationNotFound);
    }
}
