using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Messaging.Events;

public sealed record ParticipantAddedDomainEvent(
    Guid ConversationId,
    Guid UserId,
    Guid TenantId) : DomainEvent;
