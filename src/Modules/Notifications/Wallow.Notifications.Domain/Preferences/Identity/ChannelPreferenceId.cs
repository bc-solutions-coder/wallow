using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Preferences.Identity;

public readonly record struct ChannelPreferenceId(Guid Value) : IStronglyTypedId<ChannelPreferenceId>
{
    public static ChannelPreferenceId Create(Guid value) => new(value);
    public static ChannelPreferenceId New() => new(Guid.NewGuid());
}
