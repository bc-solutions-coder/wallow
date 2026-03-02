using FluentValidation;

namespace Foundry.Communications.Application.Channels.Sms.Commands.SendSms;

public sealed class SendSmsValidator : AbstractValidator<SendSmsCommand>
{
    public SendSmsValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Recipient phone number is required");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Message body is required");
    }
}
