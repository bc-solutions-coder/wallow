namespace Wallow.Messaging.Application.Conversations.Queries.GetMessages;

public sealed record GetMessagesQuery(Guid ConversationId, Guid UserId, Guid? CursorMessageId, int PageSize);
