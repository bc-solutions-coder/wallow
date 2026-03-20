using FluentValidation;

namespace Wallow.Messaging.Application.Conversations.Commands.CreateConversation;

public sealed class CreateConversationValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationValidator()
    {
        RuleFor(x => x.InitiatorId)
            .NotEmpty()
            .WithMessage("InitiatorId is required");

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Type is required")
            .Must(t => t is "Direct" or "Group")
            .WithMessage("Type must be 'Direct' or 'Group'");

        When(x => x.Type == "Direct", () =>
        {
            RuleFor(x => x.RecipientId)
                .NotEmpty()
                .WithMessage("RecipientId is required for direct conversations");
        });

        When(x => x.Type == "Group", () =>
        {
            RuleFor(x => x.MemberIds)
                .NotEmpty()
                .WithMessage("MemberIds is required for group conversations");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required for group conversations");
        });
    }
}
