using Wallow.Notifications.Domain.Channels.Push;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IPushProviderFactory
{
    Task<IPushProvider> GetProviderAsync(DeviceRegistration device);
}
