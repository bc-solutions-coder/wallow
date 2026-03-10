using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Infrastructure.Settings;

public interface ITenantSettingRepository<TDbContext> where TDbContext : TenantAwareDbContext<TDbContext>
{
    Task<TenantSettingEntity?> GetAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSettingEntity>> GetAllAsync(
        TenantId tenantId, string moduleKey, CancellationToken cancellationToken = default);

    Task UpsertAsync(TenantSettingEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default);
}

public interface IUserSettingRepository<TDbContext> where TDbContext : TenantAwareDbContext<TDbContext>
{
    Task<UserSettingEntity?> GetAsync(
        TenantId tenantId, string userId, string moduleKey, string settingKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSettingEntity>> GetAllAsync(
        TenantId tenantId, string userId, string moduleKey, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserSettingEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId, string userId, string moduleKey, string settingKey,
        CancellationToken cancellationToken = default);
}
