using FluentValidation.TestHelper;
using Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;
using Wallow.Notifications.Domain.Channels.Push.Identity;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class DeregisterDeviceValidatorTests
{
    private readonly DeregisterDeviceValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidId()
    {
        DeregisterDeviceCommand command = new(DeviceRegistrationId.New());
        TestValidationResult<DeregisterDeviceCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_IdIsEmpty()
    {
        DeregisterDeviceCommand command = new(new DeviceRegistrationId(Guid.Empty));
        TestValidationResult<DeregisterDeviceCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DeviceRegistrationId);
    }
}
