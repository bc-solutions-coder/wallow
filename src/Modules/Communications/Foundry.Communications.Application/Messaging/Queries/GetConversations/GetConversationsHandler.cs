using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Queries.GetConversations;

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
