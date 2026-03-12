using Foundry.Shared.Kernel.Domain;

namespace Foundry.Messaging.Domain.Conversations.Events;

public sealed record ConversationCreatedDomainEvent(
    Guid ConversationId,
    Guid TenantId) : DomainEvent;
