using FluentValidation.TestHelper;
using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Tests.Application.Commands.InApp;

public class SendNotificationValidatorTests
{
    private readonly SendNotificationValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message");
        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_UserIdIsEmpty()
    {
        SendNotificationCommand command = new(Guid.Empty, NotificationType.TaskAssigned, "Title", "Message");
        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Should_Have_Error_When_TitleIsEmpty()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, string.Empty, "Message");
        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_MessageIsEmpty()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.Mention, "Title", string.Empty);
        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Should_Have_Error_When_TitleExceedsMaxLength()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.TaskAssigned, new string('a', 501), "Message");
        TestValidationResult<SendNotificationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }
}
