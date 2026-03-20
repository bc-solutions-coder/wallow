using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IPushMessageRepository
{
    Task<PushMessage?> GetByIdAsync(PushMessageId id, CancellationToken cancellationToken = default);
    void Add(PushMessage message);
    void Update(PushMessage message);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
