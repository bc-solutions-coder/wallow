using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Application.Conversations.Queries.GetUnreadConversationCount;

public sealed class GetUnreadConversationCountHandler(IMessagingQueryService messagingQueryService)
{
    public async Task<Result<int>> Handle(
        GetUnreadConversationCountQuery query,
        CancellationToken cancellationToken)
    {
        int count = await messagingQueryService.GetUnreadConversationCountAsync(
            query.UserId,
            cancellationToken);

        return Result.Success(count);
    }
}
