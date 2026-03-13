using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Push.Commands.RegisterDevice;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class RegisterDeviceValidatorTests
{
    private readonly RegisterDeviceValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        RegisterDeviceCommand command = new(new UserId(Guid.NewGuid()), TenantId.New(), PushPlatform.Fcm, "device-token");
        TestValidationResult<RegisterDeviceCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_TokenIsEmpty()
    {
        RegisterDeviceCommand command = new(new UserId(Guid.NewGuid()), TenantId.New(), PushPlatform.Fcm, string.Empty);
        TestValidationResult<RegisterDeviceCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Token);
    }
}
