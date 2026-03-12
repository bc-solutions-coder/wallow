using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Messaging.Infrastructure.Persistence.Repositories;

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
