using Foundry.Communications.Domain.Channels.Email.Entities;

namespace Foundry.Communications.Application.Channels.Email.Interfaces;

public interface IEmailMessageRepository
{
    void Add(EmailMessage emailMessage);
    Task<IReadOnlyList<EmailMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailMessage>> GetFailedRetryableAsync(int maxRetries, int limit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
