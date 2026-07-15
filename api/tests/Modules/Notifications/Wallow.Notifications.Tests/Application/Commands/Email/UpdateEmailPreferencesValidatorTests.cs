using FluentValidation.TestHelper;
using Wallow.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Tests.Application.Commands.Email;

public class UpdateEmailPreferencesValidatorTests
{
    private readonly UpdateEmailPreferencesValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        UpdateEmailPreferencesCommand command = new(Guid.NewGuid(), NotificationType.TaskAssigned, true);
        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_UserIdIsEmpty()
    {
        UpdateEmailPreferencesCommand command = new(Guid.Empty, NotificationType.TaskAssigned, true);
        TestValidationResult<UpdateEmailPreferencesCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
