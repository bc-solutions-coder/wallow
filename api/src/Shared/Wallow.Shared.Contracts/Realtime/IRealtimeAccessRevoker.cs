namespace Wallow.Shared.Contracts.Realtime;

/// <summary>
/// Ends the realtime connections a person holds against one tenant.
///
/// A live SSE stream or hub connection carries the roles and permissions it was opened with, so
/// it keeps delivering tenant traffic long after the credential behind it stopped being valid.
/// Revoking a token does not reach an already-open socket; this does.
/// </summary>
public interface IRealtimeAccessRevoker
{
    Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default);
}
