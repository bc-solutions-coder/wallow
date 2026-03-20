using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Channels.Sms.Identity;

public readonly record struct SmsPreferenceId(Guid Value) : IStronglyTypedId<SmsPreferenceId>
{
    public static SmsPreferenceId Create(Guid value) => new(value);
    public static SmsPreferenceId New() => new(Guid.NewGuid());
}
