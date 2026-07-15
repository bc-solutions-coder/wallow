using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Repository for managing API scopes.
/// </summary>
public interface IApiScopeRepository
{
    /// <summary>
    /// Gets all API scopes, optionally filtered by category.
    /// </summary>
    Task<IReadOnlyList<ApiScope>> GetAllAsync(string? category = null, CancellationToken ct = default);

    /// <summary>
    /// Gets API scopes by their codes.
    /// </summary>
    Task<IReadOnlyList<ApiScope>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);

    /// <summary>
    /// Adds a new API scope.
    /// </summary>
    void Add(ApiScope scope);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
