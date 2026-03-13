using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class SetTenantPushEnabledValidatorTests
{
    private readonly SetTenantPushEnabledValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SetTenantPushEnabledCommand command = new(TenantId.New(), PushPlatform.Fcm, true);
        TestValidationResult<SetTenantPushEnabledCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_DisablingWithValidPlatform()
    {
        SetTenantPushEnabledCommand command = new(TenantId.New(), PushPlatform.Apns, false);
        TestValidationResult<SetTenantPushEnabledCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
