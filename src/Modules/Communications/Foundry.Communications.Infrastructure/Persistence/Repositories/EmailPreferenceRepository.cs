using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class EmailPreferenceRepository : IEmailPreferenceRepository
{
    private readonly CommunicationsDbContext _context;

    public EmailPreferenceRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(EmailPreference preference)
    {
        _context.EmailPreferences.Add(preference);
    }

    public Task<EmailPreference?> GetByIdAsync(EmailPreferenceId id, CancellationToken cancellationToken = default)
    {
        return _context.EmailPreferences
            .AsTracking()
            .FirstOrDefaultAsync(ep => ep.Id == id, cancellationToken);
    }

    public Task<EmailPreference?> GetByUserAndTypeAsync(Guid userId, NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        return _context.EmailPreferences
            .AsTracking()
            .FirstOrDefaultAsync(
                ep => ep.UserId == userId && ep.NotificationType == notificationType,
                cancellationToken);
    }

    public async Task<IReadOnlyList<EmailPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.EmailPreferences
            .Where(ep => ep.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
