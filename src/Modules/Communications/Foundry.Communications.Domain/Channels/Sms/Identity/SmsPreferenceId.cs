using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Domain.Channels.Sms.Identity;

public readonly record struct SmsPreferenceId(Guid Value) : IStronglyTypedId<SmsPreferenceId>
{
    public static SmsPreferenceId Create(Guid value) => new(value);
    public static SmsPreferenceId New() => new(Guid.NewGuid());
}
