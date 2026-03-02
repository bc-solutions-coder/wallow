namespace Foundry.Communications.Application.Messaging.Commands.CreateConversation;

public sealed record CreateConversationCommand(
    Guid InitiatorId,
    Guid? RecipientId,
    IReadOnlyList<Guid>? MemberIds,
    string Type,
    string? Name);
