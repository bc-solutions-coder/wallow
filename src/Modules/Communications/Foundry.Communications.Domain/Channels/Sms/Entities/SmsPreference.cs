using Foundry.Communications.Domain.Channels.Sms.Identity;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Channels.Sms.Entities;

public sealed class SmsPreference : Entity<SmsPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; } = null!;
    public bool IsOptedIn { get; init; }

    private SmsPreference() { }

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
