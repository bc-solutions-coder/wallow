namespace Wallow.Shared.Contracts.Realtime;

/// <summary>
/// Ends existing SSE and hub connections after access is revoked. Token revocation alone
/// does not close connections authenticated when they opened.
/// </summary>
public interface IRealtimeAccessRevoker
{
    /// <summary>Ends the connections a person holds against one tenant.</summary>
    Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Ends every connection opened with a token the named client was issued, whoever holds it.</summary>
    Task RevokeClientAsync(string clientId, CancellationToken ct = default);
}
