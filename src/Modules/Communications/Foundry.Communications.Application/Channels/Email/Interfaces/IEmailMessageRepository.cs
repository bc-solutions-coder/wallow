using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Identity;

namespace Foundry.Communications.Application.Channels.Email.Interfaces;

public interface IEmailMessageRepository
{
    void Add(EmailMessage emailMessage);
    Task<EmailMessage?> GetByIdAsync(EmailMessageId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailMessage>> GetFailedRetryableAsync(int maxRetries, int limit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
