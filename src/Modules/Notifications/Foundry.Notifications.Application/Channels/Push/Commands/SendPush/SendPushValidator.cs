using FluentValidation;

namespace Foundry.Notifications.Application.Channels.Push.Commands.SendPush;

public sealed class SendPushValidator : AbstractValidator<SendPushCommand>
{
    public SendPushValidator()
    {
        RuleFor(x => x.RecipientId.Value)
            .NotEmpty().WithMessage("Recipient is required");

        RuleFor(x => x.TenantId.Value)
            .NotEmpty().WithMessage("Tenant is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .MaximumLength(4000).WithMessage("Body cannot exceed 4000 characters");

        RuleFor(x => x.NotificationType)
            .NotEmpty().WithMessage("Notification type is required");
    }
}
