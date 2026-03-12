using FluentValidation;

namespace Foundry.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

public sealed class DeregisterDeviceValidator : AbstractValidator<DeregisterDeviceCommand>
{
    public DeregisterDeviceValidator()
    {
        RuleFor(x => x.DeviceRegistrationId)
            .Must(x => x.Value != Guid.Empty).WithMessage("Device registration ID is required");
    }
}
