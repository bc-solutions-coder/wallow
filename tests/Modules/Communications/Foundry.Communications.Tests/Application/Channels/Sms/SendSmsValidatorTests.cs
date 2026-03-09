using FluentValidation.TestHelper;
using Foundry.Communications.Application.Channels.Sms.Commands.SendSms;

namespace Foundry.Communications.Tests.Application.Channels.Sms;

public class SendSmsValidatorTests
{
    private readonly SendSmsValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_To_Is_Empty()
    {
        SendSmsCommand command = new("", "Hello world");

        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.To)
            .WithErrorMessage("Recipient phone number is required");
    }

    [Fact]
    public void Should_Have_Error_When_Body_Is_Empty()
    {
        SendSmsCommand command = new("+15551234567", "");

        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Body)
            .WithErrorMessage("Message body is required");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_From_Is_Null()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        TestValidationResult<SendSmsCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
