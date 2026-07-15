using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Channels.Email.Identity;

public readonly record struct EmailPreferenceId(Guid Value) : IStronglyTypedId<EmailPreferenceId>
{
    public static EmailPreferenceId Create(Guid value) => new(value);
    public static EmailPreferenceId New() => new(Guid.NewGuid());
}
