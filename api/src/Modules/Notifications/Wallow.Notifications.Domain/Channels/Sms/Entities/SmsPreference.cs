using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Notifications.Domain.Channels.Sms.ValueObjects;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Domain.Channels.Sms.Entities;

public sealed class SmsPreference : Entity<SmsPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; } = null!;
    public bool IsOptedIn { get; init; }

    // ReSharper disable once UnusedMember.Local
    private SmsPreference() { } // EF Core

    private SmsPreference(
        Guid userId,
        PhoneNumber phoneNumber,
        bool isOptedIn)
        : base(SmsPreferenceId.New())
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        IsOptedIn = isOptedIn;
    }

    public static SmsPreference Create(
        Guid userId,
        PhoneNumber phoneNumber,
        bool isOptedIn = true)
    {
        return new SmsPreference(userId, phoneNumber, isOptedIn);
    }

}
