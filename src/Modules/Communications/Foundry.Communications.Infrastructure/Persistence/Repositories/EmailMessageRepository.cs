using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class EmailMessageRepository(CommunicationsDbContext context) : IEmailMessageRepository
{

    public void Add(EmailMessage emailMessage)
    {
        context.EmailMessages.Add(emailMessage);
    }

    public async Task<IReadOnlyList<EmailMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await context.EmailMessages
            .AsTracking()
            .Where(e => e.Status == EmailStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailMessage>> GetFailedRetryableAsync(int maxRetries, int limit, CancellationToken cancellationToken = default)
    {
        return await context.EmailMessages
            .AsTracking()
            .Where(e => e.Status == EmailStatus.Failed && e.RetryCount < maxRetries)
            .OrderBy(e => e.RetryCount)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
