using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Domain.Channels.Sms.Identity;

public readonly record struct SmsMessageId(Guid Value) : IStronglyTypedId<SmsMessageId>
{
    public static SmsMessageId Create(Guid value) => new(value);
    public static SmsMessageId New() => new(Guid.NewGuid());
}
