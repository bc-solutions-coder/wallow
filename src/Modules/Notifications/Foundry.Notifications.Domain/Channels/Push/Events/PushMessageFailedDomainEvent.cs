using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.Push.Events;

public sealed record PushMessageFailedDomainEvent(
    PushMessageId MessageId,
    string Reason) : DomainEvent;
