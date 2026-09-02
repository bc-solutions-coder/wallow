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

        if (registration is null)
        {
            return Result.Failure(NotificationsErrors.DeviceRegistrationNotFound);
        }

        registration.Deactivate();
        deviceRegistrationRepository.Update(registration);
        await deviceRegistrationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
