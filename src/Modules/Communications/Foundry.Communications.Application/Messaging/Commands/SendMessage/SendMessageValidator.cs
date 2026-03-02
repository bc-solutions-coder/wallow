using FluentValidation;

namespace Foundry.Communications.Application.Messaging.Commands.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("ConversationId is required");

        RuleFor(x => x.SenderId)
            .NotEmpty()
            .WithMessage("SenderId is required");

        RuleFor(x => x.Body)
            .NotEmpty()
            .WithMessage("Body is required")
            .MaximumLength(4000)
            .WithMessage("Body must not exceed 4000 characters");
    }
}
