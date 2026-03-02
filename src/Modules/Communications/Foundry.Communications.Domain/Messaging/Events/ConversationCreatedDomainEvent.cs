using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Messaging.Events;

public sealed record ConversationCreatedDomainEvent(
    Guid ConversationId,
    Guid TenantId) : DomainEvent;
