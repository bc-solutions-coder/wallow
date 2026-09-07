using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IDeviceRegistrationRepository
{
    Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceRegistration>> GetActiveByUserAsync(UserId userId, CancellationToken cancellationToken = default);
    Task<bool> RegisterAsync(DeviceRegistration registration, CancellationToken cancellationToken = default);
    Task<bool> SaveDeactivationAsync(DeviceRegistration registration, CancellationToken cancellationToken = default);
}
