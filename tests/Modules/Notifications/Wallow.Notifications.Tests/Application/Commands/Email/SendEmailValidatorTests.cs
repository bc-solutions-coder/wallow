using FluentValidation.TestHelper;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;

namespace Wallow.Notifications.Tests.Application.Commands.Email;

public class SendEmailValidatorTests
{
    private readonly SendEmailValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SendEmailCommand command = new("user@test.com", null, "Subject", "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_ToIsEmpty()
    {
        SendEmailCommand command = new(string.Empty, null, "Subject", "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Should_Have_Error_When_ToIsInvalidEmail()
    {
        SendEmailCommand command = new("not-an-email", null, "Subject", "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Should_Have_Error_When_FromIsInvalidEmail()
    {
        SendEmailCommand command = new("user@test.com", "bad-email", "Subject", "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.From);
    }

    [Fact]
    public void Should_Not_Have_Error_When_FromIsNull()
    {
        SendEmailCommand command = new("user@test.com", null, "Subject", "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.From);
    }

    [Fact]
    public void Should_Have_Error_When_SubjectIsEmpty()
    {
        SendEmailCommand command = new("user@test.com", null, string.Empty, "Body");
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Should_Have_Error_When_BodyIsEmpty()
    {
        SendEmailCommand command = new("user@test.com", null, "Subject", string.Empty);
        TestValidationResult<SendEmailCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }
}
