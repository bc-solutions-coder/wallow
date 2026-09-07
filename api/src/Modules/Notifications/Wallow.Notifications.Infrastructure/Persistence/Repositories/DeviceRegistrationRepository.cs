using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class DeviceRegistrationRepository(NotificationsDbContext context) : IDeviceRegistrationRepository
{
    public Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default)
    {
        return context.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRegistration>> GetActiveByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await context.DeviceRegistrations
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RegisterAsync(DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        TenantId tenantId = TenantScope.Require(context.CurrentTenantId);
        int affected = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications.device_registrations AS device
                (id, tenant_id, user_id, platform, token, is_active, registered_at)
            VALUES ({registration.Id.Value}, {tenantId.Value}, {registration.UserId.Value},
                {registration.Platform.ToString()}, {registration.Token}, {registration.IsActive}, {registration.RegisteredAt})
            ON CONFLICT (token, tenant_id) DO UPDATE
            SET id = CASE WHEN device.user_id = EXCLUDED.user_id THEN device.id ELSE EXCLUDED.id END,
                user_id = EXCLUDED.user_id,
                platform = EXCLUDED.platform,
                is_active = EXCLUDED.is_active,
                registered_at = CASE WHEN device.is_active THEN device.registered_at ELSE EXCLUDED.registered_at END
            WHERE NOT device.is_active
                OR (device.user_id = EXCLUDED.user_id AND device.platform = EXCLUDED.platform)
            """, cancellationToken);
        return affected == 1;
    }

    public async Task<bool> SaveDeactivationAsync(DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        int affected = await context.DeviceRegistrations
            .Where(d => d.Id == registration.Id && d.UserId == registration.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.IsActive, registration.IsActive), cancellationToken);
        return affected == 1;
    }
}
