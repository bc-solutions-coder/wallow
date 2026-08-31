using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The self-service consent surface: the applications a user has durably consented to, and the
/// withdrawal that ends one — the authorization and every token chained to it.
/// </summary>
public interface IConnectedApplicationService
{
    /// <summary>The user's Valid permanent authorizations, one entry per record.</summary>
    Task<IReadOnlyList<ConnectedApplicationDto>> GetConnectedApplicationsAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Withdraws one consent: revokes the authorization and every token chained to it. False when
    /// the authorization does not exist, is not the caller's, or is not a permanent consent
    /// record — indistinguishable on purpose, so the endpoint can answer 404 to all three.
    /// </summary>
    Task<bool> WithdrawAsync(Guid userId, string authorizationId, CancellationToken ct = default);
}
