using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Interfaces;

public interface IPushMessageRepository
{
    Task<PushMessage?> GetByIdAsync(PushMessageId id, CancellationToken cancellationToken = default);
    void Add(PushMessage message);
    void Update(PushMessage message);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
