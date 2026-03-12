using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Notifications.Infrastructure.Persistence.Repositories;

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
