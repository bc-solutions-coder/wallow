using Wallow.Shared.Kernel.Domain;

namespace Wallow.Messaging.Domain.Conversations.Events;

public sealed record MessageSentDomainEvent(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderId,
    Guid TenantId) : DomainEvent;
