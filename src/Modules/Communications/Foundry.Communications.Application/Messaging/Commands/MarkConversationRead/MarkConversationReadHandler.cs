using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Commands.MarkConversationRead;

public sealed class MarkConversationReadHandler(IConversationRepository conversationRepository)
{
    public async Task<Result> Handle(
        MarkConversationReadCommand command,
        CancellationToken cancellationToken)
    {
        Conversation? conversation = await conversationRepository.GetByIdAsync(
            ConversationId.Create(command.ConversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure(Error.NotFound("Conversation", command.ConversationId));
        }

        conversation.MarkReadBy(command.UserId);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
