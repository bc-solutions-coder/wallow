namespace Wallow.Messaging.Application.Conversations.Commands.CreateConversation;

public sealed record CreateConversationCommand(
    Guid InitiatorId,
    Guid? RecipientId,
    IReadOnlyList<Guid>? MemberIds,
    string Type,
    string? Name);
