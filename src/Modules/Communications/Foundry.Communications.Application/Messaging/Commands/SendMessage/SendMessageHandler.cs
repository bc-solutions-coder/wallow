using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Commands.SendMessage;

public sealed class SendMessageHandler(IConversationRepository conversationRepository)
{
    public async Task<Result<Guid>> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        Conversation? conversation = await conversationRepository.GetByIdAsync(
            new ConversationId(command.ConversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Conversation", command.ConversationId));
        }

        conversation.SendMessage(command.SenderId, command.Body);

        await conversationRepository.SaveChangesAsync(cancellationToken);

        Message lastMessage = conversation.Messages[^1];
        return Result.Success(lastMessage.Id.Value);
    }
}
