using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;

public sealed class SendEmailValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Recipient email is required")
            .EmailAddress().WithMessage("Invalid recipient email format");

        RuleFor(x => x.From)
            .EmailAddress().WithMessage("Invalid sender email format")
            .When(x => !string.IsNullOrWhiteSpace(x.From));

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(500).WithMessage("Subject cannot exceed 500 characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required");
    }
}
