using Wallow.Notifications.Domain.Channels.Sms.Entities;

namespace Wallow.Notifications.Application.Channels.Sms.Interfaces;

public interface ISmsMessageRepository
{
    void Add(SmsMessage message);
    Task<IReadOnlyList<SmsMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
