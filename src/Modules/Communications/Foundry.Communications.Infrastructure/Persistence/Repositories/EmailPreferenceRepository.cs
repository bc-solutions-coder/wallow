using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class EmailPreferenceRepository : IEmailPreferenceRepository
{
    private static readonly Func<CommunicationsDbContext, Guid, NotificationType, CancellationToken, Task<EmailPreference?>>
        _getByUserAndTypeQuery = EF.CompileAsyncQuery(
            (CommunicationsDbContext ctx, Guid userId, NotificationType notificationType, CancellationToken _) =>
                ctx.EmailPreferences
                    .AsTracking()
                    .FirstOrDefault(ep => ep.UserId == userId && ep.NotificationType == notificationType));

    private readonly CommunicationsDbContext _context;

    public EmailPreferenceRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(EmailPreference preference)
    {
        _context.EmailPreferences.Add(preference);
    }

    public Task<EmailPreference?> GetByUserAndTypeAsync(Guid userId, NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        return _getByUserAndTypeQuery(_context, userId, notificationType, cancellationToken);
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
