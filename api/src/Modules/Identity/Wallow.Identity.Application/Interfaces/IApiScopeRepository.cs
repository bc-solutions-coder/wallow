using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Persistence boundary for the API scope catalog.
/// </summary>
public interface IApiScopeRepository
{
    /// <summary>
    /// Gets all API scopes, optionally filtered by category.
    /// </summary>
    Task<IReadOnlyList<ApiScope>> GetAllAsync(string? category = null, CancellationToken ct = default);

    /// <summary>
    /// Returns matching catalog entries; unknown codes are omitted.
    /// </summary>
    Task<IReadOnlyList<ApiScope>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);

    /// <summary>
    /// Tracks a new scope for the next save.
    /// </summary>
    void Add(ApiScope scope);

    /// <summary>
    /// Persists tracked scope changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
