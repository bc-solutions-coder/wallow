using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;

namespace Foundry.Communications.Application.Channels.Email.Interfaces;

public interface IEmailPreferenceRepository
{
    void Add(EmailPreference preference);
    Task<EmailPreference?> GetByUserAndTypeAsync(Guid userId, NotificationType notificationType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
