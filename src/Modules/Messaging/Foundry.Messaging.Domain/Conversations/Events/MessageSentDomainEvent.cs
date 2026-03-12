using Foundry.Shared.Kernel.Domain;

namespace Foundry.Messaging.Domain.Conversations.Events;

public sealed record MessageSentDomainEvent(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderId,
    Guid TenantId) : DomainEvent;
