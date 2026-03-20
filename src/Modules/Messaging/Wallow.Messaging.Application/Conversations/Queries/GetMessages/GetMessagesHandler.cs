using Wallow.Messaging.Application.Conversations.DTOs;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Application.Conversations.Queries.GetMessages;

public sealed class GetMessagesHandler(IMessagingQueryService messagingQueryService)
{
    public async Task<Result<IReadOnlyList<MessageDto>>> Handle(
        GetMessagesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MessageDto> messages = await messagingQueryService.GetMessagesAsync(
            query.ConversationId,
            query.UserId,
            query.CursorMessageId,
            query.PageSize,
            cancellationToken);

        return Result.Success(messages);
    }
}
