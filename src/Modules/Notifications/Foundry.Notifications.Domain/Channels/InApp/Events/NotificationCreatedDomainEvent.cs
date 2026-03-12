using Foundry.Shared.Kernel.Domain;

namespace Foundry.Notifications.Domain.Channels.InApp.Events;

public sealed record NotificationCreatedDomainEvent(
    Guid NotificationId,
    Guid UserId,
    string Title,
    string Type) : DomainEvent;
