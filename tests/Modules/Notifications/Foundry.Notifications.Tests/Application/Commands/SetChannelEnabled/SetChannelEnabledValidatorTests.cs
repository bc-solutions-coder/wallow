using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;
using Foundry.Notifications.Domain.Preferences;

namespace Foundry.Notifications.Tests.Application.Commands.SetChannelEnabled;

public class SetChannelEnabledValidatorTests
{
    private readonly SetChannelEnabledValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SetChannelEnabledCommand command = new(
            UserId: Guid.NewGuid(),
            ChannelType: ChannelType.Email,
            IsEnabled: true,
            NotificationType: "*");

        TestValidationResult<SetChannelEnabledCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_UserIdIsEmpty()
    {
        SetChannelEnabledCommand command = new(
            UserId: Guid.Empty,
            ChannelType: ChannelType.Email,
            IsEnabled: true,
            NotificationType: "*");

        TestValidationResult<SetChannelEnabledCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Should_Have_Error_When_NotificationTypeIsEmpty()
    {
        SetChannelEnabledCommand command = new(
            UserId: Guid.NewGuid(),
            ChannelType: ChannelType.Email,
            IsEnabled: true,
            NotificationType: "");

        TestValidationResult<SetChannelEnabledCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.NotificationType);
    }
}
