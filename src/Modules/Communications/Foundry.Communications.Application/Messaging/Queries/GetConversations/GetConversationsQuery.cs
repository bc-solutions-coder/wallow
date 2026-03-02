namespace Foundry.Communications.Application.Messaging.Queries.GetConversations;

public sealed record GetConversationsQuery(Guid UserId, int Page, int PageSize);
