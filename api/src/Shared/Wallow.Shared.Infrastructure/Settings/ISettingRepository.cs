using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Infrastructure.Settings;

public interface ITenantSettingRepository<TDbContext> where TDbContext : DbContext, ITenantAwareContext
{
    Task<TenantSettingEntity?> GetAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSettingEntity>> GetAllAsync(
        TenantId tenantId, string moduleKey, CancellationToken cancellationToken = default);

    Task UpsertAsync(TenantSettingEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId, string moduleKey, string settingKey, CancellationToken cancellationToken = default);
}

public interface IUserSettingRepository<TDbContext> where TDbContext : DbContext, ITenantAwareContext
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
