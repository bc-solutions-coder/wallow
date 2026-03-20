using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.InApp.Interfaces;

public interface INotificationService
{
    Task SendToUserAsync(Guid userId, string title, string message, string type, CancellationToken cancellationToken = default);

    Task BroadcastToTenantAsync(TenantId tenantId, string title, string message, string type, CancellationToken cancellationToken = default);
}
