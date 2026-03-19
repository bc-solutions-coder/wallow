using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.Application.Interfaces;

public interface IDeveloperAppService
{
    Task<DeveloperAppRegistrationResult> RegisterClientAsync(
        string clientId,
        string clientName,
        CancellationToken cancellationToken = default);
}
