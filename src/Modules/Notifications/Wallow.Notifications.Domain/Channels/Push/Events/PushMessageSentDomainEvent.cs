using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.Push.Events;

public sealed record PushMessageSentDomainEvent(
    PushMessageId MessageId) : DomainEvent;
