using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Application.Conversations.Commands.MarkConversationRead;

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
