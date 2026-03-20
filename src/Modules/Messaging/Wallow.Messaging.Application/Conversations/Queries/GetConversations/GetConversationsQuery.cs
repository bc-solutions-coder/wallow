namespace Wallow.Messaging.Application.Conversations.Queries.GetConversations;

public sealed record GetConversationsQuery(Guid UserId, int Page, int PageSize);
