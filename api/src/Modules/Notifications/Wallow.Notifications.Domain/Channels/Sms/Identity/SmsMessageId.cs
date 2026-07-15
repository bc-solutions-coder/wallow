using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Channels.Sms.Identity;

public readonly record struct SmsMessageId(Guid Value) : IStronglyTypedId<SmsMessageId>
{
    public static SmsMessageId Create(Guid value) => new(value);
    public static SmsMessageId New() => new(Guid.NewGuid());
}
