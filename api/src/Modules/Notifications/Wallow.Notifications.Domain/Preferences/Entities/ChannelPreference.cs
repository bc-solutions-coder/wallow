using Wallow.Notifications.Domain.Preferences.Events;
using Wallow.Notifications.Domain.Preferences.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Domain.Preferences.Entities;

public sealed class ChannelPreference : AggregateRoot<ChannelPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private ChannelPreference() { } // EF Core

    private ChannelPreference(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        bool isEnabled,
        TimeProvider timeProvider)
        : base(ChannelPreferenceId.New())
    {
        UserId = userId;
        ChannelType = channelType;
        NotificationType = notificationType;
        IsEnabled = isEnabled;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static ChannelPreference Create(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        TimeProvider timeProvider,
        bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(notificationType);

        ChannelPreference preference = new(userId, channelType, notificationType, isEnabled, timeProvider);

        preference.RaiseDomainEvent(new ChannelPreferenceCreatedEvent(
            preference.Id,
            userId,
            channelType,
            notificationType,
            isEnabled));

        return preference;
    }

    public void Enable(TimeProvider timeProvider)
    {
        IsEnabled = true;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Disable(TimeProvider timeProvider)
    {
        IsEnabled = false;
        SetUpdated(timeProvider.GetUtcNow());
    }

}
