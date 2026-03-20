namespace Wallow.Messaging.Application.Conversations.Commands.SendMessage;

public sealed record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string Body);
