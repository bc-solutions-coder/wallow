using Foundry.Notifications.Domain.Preferences.Identity;
using Foundry.Shared.Kernel.Domain;
using JetBrains.Annotations;

namespace Foundry.Notifications.Domain.Preferences.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelPreferenceCreatedEvent(
    ChannelPreferenceId ChannelPreferenceId,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled) : DomainEvent;
