using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Queries.GetMessages;

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
