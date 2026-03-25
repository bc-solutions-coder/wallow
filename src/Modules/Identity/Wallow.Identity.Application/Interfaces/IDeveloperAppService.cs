using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface IDeveloperAppService
{
    Task<DeveloperAppRegistrationResult> RegisterClientAsync(
        string clientId,
        string clientName,
        IReadOnlyCollection<string> requestedScopes,
        string? clientType = null,
        IReadOnlyCollection<string>? redirectUris = null,
        string? creatorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperAppInfo>> GetUserAppsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<DeveloperAppInfo?> GetUserAppAsync(
        string userId,
        string clientId,
        CancellationToken cancellationToken = default);
}
