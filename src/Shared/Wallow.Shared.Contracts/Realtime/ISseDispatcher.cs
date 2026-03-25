namespace Wallow.Shared.Contracts.Realtime;

public interface ISseDispatcher
{
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
