using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Lists permanent consent records and withdraws consent plus the user's application tokens.
/// </summary>
public interface IConnectedApplicationService
{
    /// <summary>The user's Valid permanent authorizations, one entry per record.</summary>
    Task<IReadOnlyList<ConnectedApplicationDto>> GetConnectedApplicationsAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to revoke a valid permanent consent, all the user's tokens for its application,
    /// and matching ad-hoc authorizations. Returns false for malformed IDs, missing records,
    /// wrong ownership, or records that are not valid permanent consent.
    /// </summary>
    Task<bool> WithdrawAsync(Guid userId, string authorizationId, CancellationToken ct = default);
}
