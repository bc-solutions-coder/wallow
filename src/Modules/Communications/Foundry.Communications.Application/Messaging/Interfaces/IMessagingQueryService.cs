using Foundry.Communications.Application.Messaging.DTOs;

namespace Foundry.Communications.Application.Messaging.Interfaces;

public interface IMessagingQueryService
{
    Task<int> GetUnreadConversationCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid conversationId,
        Guid userId,
        Guid? cursorMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
