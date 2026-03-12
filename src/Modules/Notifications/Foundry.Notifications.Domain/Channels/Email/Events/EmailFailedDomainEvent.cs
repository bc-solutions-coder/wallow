using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.Email.Events;

public sealed record EmailFailedDomainEvent(
    Guid EmailMessageId,
    string ToAddress,
    string FailureReason,
    int RetryCount) : DomainEvent;
