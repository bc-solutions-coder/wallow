using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Application.Conversations.Commands.SendMessage;

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
