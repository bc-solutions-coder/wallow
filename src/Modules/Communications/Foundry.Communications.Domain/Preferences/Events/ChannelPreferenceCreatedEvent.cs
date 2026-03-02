using Foundry.Communications.Domain.Preferences.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Preferences.Events;

public sealed record ChannelPreferenceCreatedEvent(
    ChannelPreferenceId ChannelPreferenceId,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled) : DomainEvent;
