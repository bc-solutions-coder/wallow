using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Application.Conversations.Queries.GetUnreadConversationCount;

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
