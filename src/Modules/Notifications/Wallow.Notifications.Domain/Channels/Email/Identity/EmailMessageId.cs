using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Channels.Email.Identity;

public readonly record struct EmailMessageId(Guid Value) : IStronglyTypedId<EmailMessageId>
{
    public static EmailMessageId Create(Guid value) => new(value);
    public static EmailMessageId New() => new(Guid.NewGuid());
}
