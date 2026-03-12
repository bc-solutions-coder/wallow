using FluentValidation;

namespace Foundry.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;

public sealed class UpdateEmailPreferencesValidator : AbstractValidator<UpdateEmailPreferencesCommand>
{
    public UpdateEmailPreferencesValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.NotificationType)
            .IsInEnum().WithMessage("Invalid notification type");
    }
}
