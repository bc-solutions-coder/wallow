using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Enums;

namespace Foundry.Notifications.Application.Channels.Email.Interfaces;

public interface IEmailPreferenceRepository
{
    void Add(EmailPreference preference);
    Task<EmailPreference?> GetByUserAndTypeAsync(Guid userId, NotificationType notificationType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
