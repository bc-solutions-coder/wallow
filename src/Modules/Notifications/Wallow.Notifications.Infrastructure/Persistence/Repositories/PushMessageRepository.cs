using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Identity;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class PushMessageRepository(NotificationsDbContext context) : IPushMessageRepository
{
    public Task<PushMessage?> GetByIdAsync(PushMessageId id, CancellationToken cancellationToken = default)
    {
        return context.PushMessages
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void Add(PushMessage message)
    {
        context.PushMessages.Add(message);
    }

    public void Update(PushMessage message)
    {
        context.PushMessages.Update(message);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
