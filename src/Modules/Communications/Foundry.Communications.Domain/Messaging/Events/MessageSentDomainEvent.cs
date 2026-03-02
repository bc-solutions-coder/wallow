using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Messaging.Events;

public sealed record MessageSentDomainEvent(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderId,
    Guid TenantId) : DomainEvent;
