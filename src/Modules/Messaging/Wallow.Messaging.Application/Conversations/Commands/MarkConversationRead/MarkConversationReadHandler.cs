using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Application.Conversations.Commands.MarkConversationRead;

public sealed class MarkConversationReadHandler(
    IConversationRepository conversationRepository,
    IMessagingQueryService messagingQueryService,
    TimeProvider timeProvider)
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

        bool isParticipant = await messagingQueryService.IsParticipantAsync(
            command.ConversationId, command.UserId, cancellationToken);

        if (!isParticipant)
        {
            return Result.Failure(Error.Unauthorized("Unauthorized access to conversation"));
        }

        conversation.MarkReadBy(command.UserId, timeProvider);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
