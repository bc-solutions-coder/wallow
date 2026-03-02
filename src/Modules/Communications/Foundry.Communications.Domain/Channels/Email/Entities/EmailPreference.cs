using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Channels.Email.Entities;

public sealed class EmailPreference : AggregateRoot<EmailPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public NotificationType NotificationType { get; private set; }
    public bool IsEnabled { get; private set; }

    private EmailPreference() { }

    private EmailPreference(
        Guid userId,
        NotificationType notificationType,
        bool isEnabled,
        TimeProvider timeProvider)
        : base(EmailPreferenceId.New())
    {
        UserId = userId;
        NotificationType = notificationType;
        IsEnabled = isEnabled;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static EmailPreference Create(
        Guid userId,
        NotificationType notificationType,
        bool isEnabled = true,
        TimeProvider? timeProvider = null)
    {
        return new EmailPreference(userId, notificationType, isEnabled, timeProvider ?? TimeProvider.System);
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

    public void Toggle(TimeProvider timeProvider)
    {
        IsEnabled = !IsEnabled;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
