using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;

namespace Foundry.Messaging.Application.Conversations.Interfaces;

public interface IConversationRepository
{
    void Add(Conversation conversation);
    Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
