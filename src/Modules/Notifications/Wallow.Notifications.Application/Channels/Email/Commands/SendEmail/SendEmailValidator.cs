using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;

public sealed class SendEmailValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Recipient email is required")
            .EmailAddress().WithMessage("Invalid recipient email format")
            .Must(email => !ContainsNewline(email))
            .WithMessage("Recipient email must not contain control characters");

        RuleFor(x => x.From)
            .Must(email => !ContainsNewline(email!))
            .WithMessage("Sender email must not contain control characters")
            .EmailAddress().WithMessage("Invalid sender email format")
            .When(x => !string.IsNullOrWhiteSpace(x.From));

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(500).WithMessage("Subject cannot exceed 500 characters")
            .Must(subject => !ContainsNewline(subject))
            .WithMessage("Subject must not contain control characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required");
    }

    private static bool ContainsNewline(string value) =>
        value.IndexOf('\r', StringComparison.Ordinal) >= 0 ||
        value.IndexOf('\n', StringComparison.Ordinal) >= 0;
}
