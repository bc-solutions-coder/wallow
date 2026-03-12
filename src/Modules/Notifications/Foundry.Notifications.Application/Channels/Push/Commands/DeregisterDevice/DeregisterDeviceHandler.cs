using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

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
            return Result.Failure(Error.NotFound("DeviceRegistration", command.DeviceRegistrationId));
        }

        registration.Deactivate();
        deviceRegistrationRepository.Update(registration);
        await deviceRegistrationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
