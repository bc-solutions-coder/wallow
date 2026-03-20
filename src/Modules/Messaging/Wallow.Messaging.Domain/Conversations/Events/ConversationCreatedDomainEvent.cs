using Wallow.Shared.Kernel.Domain;

namespace Wallow.Messaging.Domain.Conversations.Events;

public sealed record ConversationCreatedDomainEvent(
    Guid ConversationId,
    Guid TenantId) : DomainEvent;
