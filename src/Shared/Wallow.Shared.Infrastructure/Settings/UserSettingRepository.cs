using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Infrastructure.Settings;

public sealed class UserSettingRepository<TDbContext>(TDbContext context)
    : IUserSettingRepository<TDbContext>
    where TDbContext : TenantAwareDbContext<TDbContext>
{
    public Task<UserSettingEntity?> GetAsync(
        TenantId tenantId, string userId, string moduleKey, string settingKey,
        CancellationToken cancellationToken = default)
    {
        return context.Set<UserSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.UserId == userId && e.ModuleKey == moduleKey && e.SettingKey == settingKey,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserSettingEntity>> GetAllAsync(
        TenantId tenantId, string userId, string moduleKey, CancellationToken cancellationToken = default)
    {
        return await context.Set<UserSettingEntity>()
            .Where(e => e.TenantId == tenantId && e.UserId == userId && e.ModuleKey == moduleKey)
            .OrderBy(e => e.SettingKey)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(UserSettingEntity entity, CancellationToken cancellationToken = default)
    {
        UserSettingEntity? existing = await context.Set<UserSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == entity.TenantId && e.UserId == entity.UserId
                     && e.ModuleKey == entity.ModuleKey && e.SettingKey == entity.SettingKey,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(entity.Value);
        }
        else
        {
            context.Set<UserSettingEntity>().Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, string userId, string moduleKey, string settingKey,
        CancellationToken cancellationToken = default)
    {
        UserSettingEntity? existing = await context.Set<UserSettingEntity>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.UserId == userId && e.ModuleKey == moduleKey && e.SettingKey == settingKey,
                cancellationToken);

        if (existing is not null)
        {
            context.Set<UserSettingEntity>().Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
