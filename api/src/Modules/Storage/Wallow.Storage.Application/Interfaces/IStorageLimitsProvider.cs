using Wallow.Storage.Application.Settings;

namespace Wallow.Storage.Application.Interfaces;

/// <summary>Resolves a tenant's effective storage limits (tenant setting overrides over code defaults).</summary>
public interface IStorageLimitsProvider
{
    Task<StorageLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
