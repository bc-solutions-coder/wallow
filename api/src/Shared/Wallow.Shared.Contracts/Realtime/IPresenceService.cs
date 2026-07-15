namespace Wallow.Shared.Contracts.Realtime;

public interface IPresenceService
{
    Task TrackConnectionAsync(Guid tenantId, string userId, string connectionId, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
    Task SetPageContextAsync(Guid tenantId, string connectionId, string pageContext, CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(Guid tenantId, string pageContext, CancellationToken ct = default);
    Task<bool> IsUserOnlineAsync(Guid tenantId, string userId, CancellationToken ct = default);
    Task<string?> GetUserIdByConnectionAsync(string connectionId, CancellationToken ct = default);
}
