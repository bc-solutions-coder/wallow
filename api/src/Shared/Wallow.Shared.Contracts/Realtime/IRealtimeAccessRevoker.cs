namespace Wallow.Shared.Contracts.Realtime;

/// <summary>
/// Ends realtime connections from outside the request serving them.
///
/// A live SSE stream or hub connection carries the roles and permissions it was opened with, so
/// it keeps delivering tenant traffic long after the credential behind it stopped being valid.
/// Revoking a token does not reach an already-open socket; this does.
/// </summary>
public interface IRealtimeAccessRevoker
{
    /// <summary>Ends the connections a person holds against one tenant.</summary>
    Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Ends every connection opened with a token the named client was issued, whoever holds it.</summary>
    Task RevokeClientAsync(string clientId, CancellationToken ct = default);
}
