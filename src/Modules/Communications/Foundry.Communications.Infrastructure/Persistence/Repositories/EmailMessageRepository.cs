using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class EmailMessageRepository : IEmailMessageRepository
{
    private readonly CommunicationsDbContext _context;

    public EmailMessageRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(EmailMessage emailMessage)
    {
        _context.EmailMessages.Add(emailMessage);
    }

    public Task<EmailMessage?> GetByIdAsync(EmailMessageId id, CancellationToken cancellationToken = default)
    {
        return _context.EmailMessages.FindAsync([id], cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<EmailMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.EmailMessages
            .Where(e => e.Status == EmailStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailMessage>> GetFailedRetryableAsync(int maxRetries, int limit, CancellationToken cancellationToken = default)
    {
        return await _context.EmailMessages
            .Where(e => e.Status == EmailStatus.Failed && e.RetryCount < maxRetries)
            .OrderBy(e => e.RetryCount)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
