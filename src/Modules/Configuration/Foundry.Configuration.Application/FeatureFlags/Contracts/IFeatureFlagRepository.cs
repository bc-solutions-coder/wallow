using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Pagination;

namespace Foundry.Configuration.Application.FeatureFlags.Contracts;

public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByIdAsync(FeatureFlagId id, CancellationToken ct = default);
    Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<FeatureFlag>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(FeatureFlag flag, CancellationToken ct = default);
    Task UpdateAsync(FeatureFlag flag, CancellationToken ct = default);
    Task DeleteAsync(FeatureFlag flag, CancellationToken ct = default);
}
