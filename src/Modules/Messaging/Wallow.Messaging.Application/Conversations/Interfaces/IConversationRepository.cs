using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;

namespace Wallow.Messaging.Application.Conversations.Interfaces;

public interface IConversationRepository
{
    void Add(Conversation conversation);
    Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
