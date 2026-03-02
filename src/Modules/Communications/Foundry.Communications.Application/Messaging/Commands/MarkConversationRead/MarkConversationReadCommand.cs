namespace Foundry.Communications.Application.Messaging.Commands.MarkConversationRead;

public sealed record MarkConversationReadCommand(Guid ConversationId, Guid UserId);
