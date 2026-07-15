using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IPushProviderFactory
{
    Task<IPushProvider> GetProviderAsync(PushPlatform platform);
}
