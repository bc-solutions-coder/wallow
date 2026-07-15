using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Email.Events;

public sealed record EmailFailedDomainEvent(
    Guid EmailMessageId,
    string ToAddress,
    string FailureReason,
    int RetryCount) : DomainEvent;
