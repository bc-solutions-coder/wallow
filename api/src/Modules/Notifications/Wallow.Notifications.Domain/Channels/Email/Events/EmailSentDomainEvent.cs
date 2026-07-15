using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.Events;

public sealed record EmailSentDomainEvent(
    Guid EmailMessageId,
    string ToAddress,
    string Subject) : DomainEvent;
