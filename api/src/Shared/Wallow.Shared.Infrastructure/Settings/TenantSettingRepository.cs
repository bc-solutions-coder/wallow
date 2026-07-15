using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Infrastructure.Settings;

public sealed class TenantSettingRepository<TDbContext>(TDbContext context)
    : ITenantSettingRepository<TDbContext>
    where TDbContext : TenantAwareDbContext<TDbContext>
{
    public Task<TenantSettingEntity?> GetAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default)
    {
        return context.Set<TenantSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ModuleKey == moduleKey && e.SettingKey == settingKey,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSettingEntity>> GetAllAsync(
        TenantId tenantId, string moduleKey, CancellationToken cancellationToken = default)
    {
        return await context.Set<TenantSettingEntity>()
            .Where(e => e.TenantId == tenantId && e.ModuleKey == moduleKey)
            .OrderBy(e => e.SettingKey)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(TenantSettingEntity entity, CancellationToken cancellationToken = default)
    {
        TenantSettingEntity? existing = await context.Set<TenantSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == entity.TenantId && e.ModuleKey == entity.ModuleKey && e.SettingKey == entity.SettingKey,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(entity.Value);
        }
        else
        {
            if (SettingKeyValidator.IsCustomKey(entity.SettingKey))
            {
                int currentCount = await context.Set<TenantSettingEntity>()
                    .CountAsync(e => e.TenantId == entity.TenantId
                                     && e.SettingKey.StartsWith(SettingKeyValidator.CustomPrefix), cancellationToken);

                if (currentCount >= SettingKeyValidator.MaxCustomKeysPerTenant)
                {
                    throw new InvalidOperationException(
                        $"Tenant has reached the maximum of {SettingKeyValidator.MaxCustomKeysPerTenant} setting keys.");
                }
            }

            context.Set<TenantSettingEntity>().Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default)
    {
        TenantSettingEntity? existing = await context.Set<TenantSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ModuleKey == moduleKey && e.SettingKey == settingKey,
                cancellationToken);

        if (existing is not null)
        {
            context.Set<TenantSettingEntity>().Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
