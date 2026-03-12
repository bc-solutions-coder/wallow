using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Application.Conversations.Commands.SendMessage;

public sealed class SendMessageHandler(
    IConversationRepository conversationRepository,
    TimeProvider timeProvider)
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

        conversation.SendMessage(command.SenderId, command.Body, timeProvider);

        await conversationRepository.SaveChangesAsync(cancellationToken);

        Message lastMessage = conversation.Messages[^1];
        return Result.Success(lastMessage.Id.Value);
    }
}
