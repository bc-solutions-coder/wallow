using Microsoft.EntityFrameworkCore;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;

namespace Wallow.Messaging.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(MessagingDbContext context) : IConversationRepository
{
    public void Add(Conversation conversation)
    {
        context.Conversations.Add(conversation);
    }

    public Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        return context.Conversations
            .AsTracking()
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
