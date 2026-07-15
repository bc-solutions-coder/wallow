using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IDeviceRegistrationRepository
{
    Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceRegistration>> GetActiveByUserAsync(UserId userId, CancellationToken cancellationToken = default);
    void Add(DeviceRegistration registration);
    void Update(DeviceRegistration registration);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
