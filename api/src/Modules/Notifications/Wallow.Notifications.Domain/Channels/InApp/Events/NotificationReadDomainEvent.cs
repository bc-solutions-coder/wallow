using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.InApp.Events;

public sealed record NotificationReadDomainEvent(
    Guid NotificationId,
    Guid UserId) : DomainEvent;
