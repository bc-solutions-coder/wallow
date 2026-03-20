using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;

public sealed class SetChannelEnabledValidator : AbstractValidator<SetChannelEnabledCommand>
{
    public SetChannelEnabledValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.NotificationType)
            .NotEmpty().WithMessage("Notification type is required");
    }
}
