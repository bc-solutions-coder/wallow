using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly CommunicationsDbContext _context;

    public ConversationRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(Conversation conversation)
    {
        _context.Conversations.Add(conversation);
    }

    public Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
