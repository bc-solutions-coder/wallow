namespace Foundry.Communications.Application.Messaging.Commands.SendMessage;

public sealed record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string Body);
