using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.Email.Events;

public sealed record EmailSentDomainEvent(
    Guid EmailMessageId,
    string ToAddress,
    string Subject) : DomainEvent;
