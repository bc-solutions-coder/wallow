using Foundry.Messaging.Application.Conversations.DTOs;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Application.Conversations.Queries.GetConversations;

public sealed class GetConversationsHandler(IMessagingQueryService messagingQueryService)
{
    public async Task<Result<IReadOnlyList<ConversationDto>>> Handle(
        GetConversationsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConversationDto> conversations = await messagingQueryService.GetConversationsAsync(
            query.UserId,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Result.Success(conversations);
    }
}
