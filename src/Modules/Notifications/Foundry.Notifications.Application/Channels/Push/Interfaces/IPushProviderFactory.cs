using Foundry.Notifications.Domain.Channels.Push.Enums;

namespace Foundry.Notifications.Application.Channels.Push.Interfaces;

public interface IPushProviderFactory
{
    IPushProvider GetProvider(PushPlatform platform);
}
