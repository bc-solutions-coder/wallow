using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;

public sealed class RegisterDeviceHandler(
    IDeviceRegistrationRepository deviceRegistrationRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            command.UserId,
            command.TenantId,
            command.Platform,
            command.Token,
            timeProvider.GetUtcNow());

        bool registered = await deviceRegistrationRepository.RegisterAsync(registration, cancellationToken);
        return registered ? Result.Success() : Result.Failure(NotificationsErrors.DeviceRegistrationConflict);
    }
}
