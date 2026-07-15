using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class EmailPreferenceRepository(NotificationsDbContext context) : IEmailPreferenceRepository
{
    private static readonly Func<NotificationsDbContext, Guid, NotificationType, CancellationToken, Task<EmailPreference?>>
        _getByUserAndTypeQuery = EF.CompileAsyncQuery(
            (NotificationsDbContext ctx, Guid userId, NotificationType notificationType, CancellationToken _) =>
                ctx.EmailPreferences
                    .AsTracking()
                    .FirstOrDefault(ep => ep.UserId == userId && ep.NotificationType == notificationType));


    public void Add(EmailPreference preference)
    {
        context.EmailPreferences.Add(preference);
    }

    public Task<EmailPreference?> GetByUserAndTypeAsync(Guid userId, NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        return _getByUserAndTypeQuery(context, userId, notificationType, cancellationToken);
    }

    public async Task<IReadOnlyList<EmailPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.EmailPreferences
            .Where(ep => ep.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
