using Foundry.Communications.Application.Preferences.Interfaces;
using Foundry.Communications.Domain.Preferences;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Communications.Domain.Preferences.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class ChannelPreferenceRepository : IChannelPreferenceRepository
{
    private readonly CommunicationsDbContext _context;

    public ChannelPreferenceRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public Task<ChannelPreference?> GetByIdAsync(ChannelPreferenceId id, CancellationToken cancellationToken = default)
    {
        return _context.ChannelPreferences.FindAsync([id], cancellationToken).AsTask();
    }

    public Task<ChannelPreference?> GetByUserAndChannelAsync(Guid userId, ChannelType channelType, CancellationToken cancellationToken = default)
    {
        return _context.ChannelPreferences
            .FirstOrDefaultAsync(
                cp => cp.UserId == userId && cp.ChannelType == channelType,
                cancellationToken);
    }

    public Task<ChannelPreference?> GetByUserChannelAndNotificationTypeAsync(Guid userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default)
    {
        return _context.ChannelPreferences
            .FirstOrDefaultAsync(
                cp => cp.UserId == userId && cp.ChannelType == channelType && cp.NotificationType == notificationType,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ChannelPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ChannelPreferences
            .Where(cp => cp.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public void Add(ChannelPreference preference)
    {
        _context.ChannelPreferences.Add(preference);
    }

    public void Update(ChannelPreference preference)
    {
        _context.ChannelPreferences.Update(preference);
    }

    public void Delete(ChannelPreference preference)
    {
        _context.ChannelPreferences.Remove(preference);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
