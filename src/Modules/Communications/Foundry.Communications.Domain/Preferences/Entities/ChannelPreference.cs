using Foundry.Communications.Domain.Preferences.Events;
using Foundry.Communications.Domain.Preferences.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Preferences.Entities;

public sealed class ChannelPreference : AggregateRoot<ChannelPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    private ChannelPreference() { }

    private ChannelPreference(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        bool isEnabled)
        : base(ChannelPreferenceId.New())
    {
        UserId = userId;
        ChannelType = channelType;
        NotificationType = notificationType;
        IsEnabled = isEnabled;
        SetCreated();
    }

    public static ChannelPreference Create(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(notificationType);

        ChannelPreference preference = new(userId, channelType, notificationType, isEnabled);

        preference.RaiseDomainEvent(new ChannelPreferenceCreatedEvent(
            preference.Id,
            userId,
            channelType,
            notificationType,
            isEnabled));

        return preference;
    }

    public void Enable()
    {
        IsEnabled = true;
        SetUpdated();
    }

    public void Disable()
    {
        IsEnabled = false;
        SetUpdated();
    }

    public void Toggle()
    {
        IsEnabled = !IsEnabled;
        SetUpdated();
    }
}
