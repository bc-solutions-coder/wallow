using FluentValidation.TestHelper;
using Foundry.Communications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Validators;

public class SendNotificationValidatorTests
{
    private readonly SendNotificationValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        SendNotificationCommand command = new(Guid.Empty, NotificationType.SystemAlert, "Title", "Message");

        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, "", "Message");

        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_MaxLength()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, new string('A', 501), "Message");

        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must not exceed 500 characters");
    }

    [Fact]
    public void Should_Have_Error_When_Message_Is_Empty()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, "Title", "");

        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message is required");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message");

        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
