using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.InApp.Events;

public sealed record NotificationReadDomainEvent(
    Guid NotificationId,
    Guid UserId) : DomainEvent;
