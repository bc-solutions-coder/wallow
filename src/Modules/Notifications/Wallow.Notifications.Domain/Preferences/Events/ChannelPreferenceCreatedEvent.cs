using Wallow.Notifications.Domain.Preferences.Identity;
using Wallow.Shared.Kernel.Domain;
using JetBrains.Annotations;

namespace Wallow.Notifications.Domain.Preferences.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelPreferenceCreatedEvent(
    ChannelPreferenceId ChannelPreferenceId,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled) : DomainEvent;
