using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Enums;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class SmsMessageRepository(NotificationsDbContext context) : ISmsMessageRepository
{

    public void Add(SmsMessage message)
    {
        context.SmsMessages.Add(message);
    }

    public async Task<IReadOnlyList<SmsMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await context.SmsMessages
            .AsTracking()
            .Where(e => e.Status == SmsStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
