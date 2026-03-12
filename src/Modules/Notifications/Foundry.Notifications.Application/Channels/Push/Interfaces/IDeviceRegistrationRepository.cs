using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Interfaces;

public interface IDeviceRegistrationRepository
{
    Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceRegistration>> GetActiveByUserAsync(UserId userId, CancellationToken cancellationToken = default);
    void Add(DeviceRegistration registration);
    void Update(DeviceRegistration registration);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
