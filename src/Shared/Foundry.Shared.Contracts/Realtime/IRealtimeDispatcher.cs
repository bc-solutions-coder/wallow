namespace Foundry.Shared.Contracts.Realtime;

public interface IRealtimeDispatcher
{
    Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default);
    Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default);
}
