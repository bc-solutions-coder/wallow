using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class TenantPushConfigurationRepository(NotificationsDbContext context) : ITenantPushConfigurationRepository
{
    public Task<TenantPushConfiguration?> GetAsync(CancellationToken cancellationToken = default)
    {
        return context.TenantPushConfigurations
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TenantPushConfiguration?> GetByPlatformAsync(PushPlatform platform, CancellationToken cancellationToken = default)
    {
        return context.TenantPushConfigurations
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Platform == platform, cancellationToken);
    }

    public async Task UpsertAsync(TenantPushConfiguration configuration, CancellationToken cancellationToken = default)
    {
        bool exists = await context.TenantPushConfigurations
            .AnyAsync(c => c.Id == configuration.Id, cancellationToken);

        if (exists)
        {
            context.TenantPushConfigurations.Update(configuration);
        }
        else
        {
            context.TenantPushConfigurations.Add(configuration);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByPlatformAsync(PushPlatform platform, CancellationToken cancellationToken = default)
    {
        await context.TenantPushConfigurations
            .Where(c => c.Platform == platform)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
