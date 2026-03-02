using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;

namespace Foundry.Communications.Application.Messaging.Interfaces;

public interface IConversationRepository
{
    void Add(Conversation conversation);
    Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
