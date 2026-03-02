using Foundry.Communications.Application.Channels.Sms.Interfaces;
using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.Enums;
using Foundry.Communications.Domain.Channels.Sms.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class SmsMessageRepository : ISmsMessageRepository
{
    private readonly CommunicationsDbContext _context;

    public SmsMessageRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(SmsMessage message)
    {
        _context.SmsMessages.Add(message);
    }

    public Task<SmsMessage?> GetByIdAsync(SmsMessageId id, CancellationToken cancellationToken = default)
    {
        return _context.SmsMessages.FindAsync([id], cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<SmsMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.SmsMessages
            .Where(e => e.Status == SmsStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
