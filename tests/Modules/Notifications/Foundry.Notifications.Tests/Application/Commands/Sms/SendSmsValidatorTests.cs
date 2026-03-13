using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Sms.Commands.SendSms;

namespace Foundry.Notifications.Tests.Application.Commands.Sms;

public class SendSmsValidatorTests
{
    private readonly SendSmsValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SendSmsCommand command = new("+12025550100", "Hello!");
        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_ToIsEmpty()
    {
        SendSmsCommand command = new(string.Empty, "Hello!");
        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Should_Have_Error_When_BodyIsEmpty()
    {
        SendSmsCommand command = new("+12025550100", string.Empty);
        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }
}
