using FluentValidation.TestHelper;
using Foundry.Communications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Tests.Application.Channels.Email.Validators;

public class UpdateEmailPreferencesValidatorTests
{
    private readonly UpdateEmailPreferencesValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        UpdateEmailPreferencesCommand command = new(Guid.Empty, NotificationType.SystemNotification, true);

        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_NotificationType_Is_Invalid()
    {
        UpdateEmailPreferencesCommand command = new(Guid.NewGuid(), (NotificationType)999, true);

        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.NotificationType)
            .WithErrorMessage("Invalid notification type");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        UpdateEmailPreferencesCommand command = new(Guid.NewGuid(), NotificationType.TaskAssigned, true);

        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(NotificationType.TaskAssigned)]
    [InlineData(NotificationType.TaskCompleted)]
    [InlineData(NotificationType.BillingInvoice)]
    [InlineData(NotificationType.SystemNotification)]
    public void Should_Not_Have_Error_For_Valid_NotificationTypes(NotificationType type)
    {
        UpdateEmailPreferencesCommand command = new(Guid.NewGuid(), type, false);

        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
