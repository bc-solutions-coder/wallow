using Wallow.Shared.Kernel.Domain;

namespace Wallow.Notifications.Domain.Channels.InApp.Events;

public sealed record NotificationCreatedDomainEvent(
    Guid NotificationId,
    Guid UserId,
    string Title,
    string Type) : DomainEvent;
