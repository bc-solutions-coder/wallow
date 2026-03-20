namespace Wallow.Messaging.Application.Conversations.Commands.MarkConversationRead;

public sealed record MarkConversationReadCommand(Guid ConversationId, Guid UserId);
