namespace Foundry.Communications.Application.Messaging.Queries.GetMessages;

public sealed record GetMessagesQuery(Guid ConversationId, Guid UserId, Guid? CursorMessageId, int PageSize);
