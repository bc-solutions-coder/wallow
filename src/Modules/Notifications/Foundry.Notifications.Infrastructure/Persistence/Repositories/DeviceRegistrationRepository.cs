using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Notifications.Infrastructure.Persistence.Repositories;

public sealed class DeviceRegistrationRepository(NotificationsDbContext context) : IDeviceRegistrationRepository
{
    public Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default)
    {
        return context.DeviceRegistrations
            .AsTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRegistration>> GetActiveByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await context.DeviceRegistrations
            .AsTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);
    }

    public void Add(DeviceRegistration registration)
    {
        context.DeviceRegistrations.Add(registration);
    }

    public void Update(DeviceRegistration registration)
    {
        context.DeviceRegistrations.Update(registration);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
