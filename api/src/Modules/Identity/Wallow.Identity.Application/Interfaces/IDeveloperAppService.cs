using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Describes a registered client to the consent screen. Registration itself lives on
/// <see cref="IOrganizationClientService"/>.
/// </summary>
public interface IDeveloperAppService
{
    Task<ConsentInfoDto?> GetConsentInfoAsync(
        string clientId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken = default);
}
