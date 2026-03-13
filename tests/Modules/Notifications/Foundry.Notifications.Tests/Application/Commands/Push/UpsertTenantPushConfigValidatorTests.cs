using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class UpsertTenantPushConfigValidatorTests
{
    private readonly UpsertTenantPushConfigValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        UpsertTenantPushConfigCommand command = new(TenantId.New(), PushPlatform.Fcm, "credentials");
        TestValidationResult<UpsertTenantPushConfigCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_CredentialsIsEmpty()
    {
        UpsertTenantPushConfigCommand command = new(TenantId.New(), PushPlatform.Fcm, string.Empty);
        TestValidationResult<UpsertTenantPushConfigCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RawCredentials);
    }
}
