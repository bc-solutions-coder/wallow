using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.Identity;

namespace Foundry.Communications.Application.Channels.Sms.Interfaces;

public interface ISmsMessageRepository
{
    void Add(SmsMessage message);
    Task<SmsMessage?> GetByIdAsync(SmsMessageId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmsMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
