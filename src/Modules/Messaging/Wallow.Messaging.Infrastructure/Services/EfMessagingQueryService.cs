using Microsoft.EntityFrameworkCore;
using Wallow.Messaging.Application.Conversations.DTOs;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Messaging.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Messaging.Infrastructure.Services;

public sealed class EfMessagingQueryService(IReadDbContext<MessagingDbContext> readDbContext) : IMessagingQueryService
{
    private readonly MessagingDbContext _dbContext = readDbContext.Context;

    public async Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        ConversationId convId = ConversationId.Create(conversationId);

        bool exists = await _dbContext.Participants
            .AnyAsync(p => p.ConversationId == convId
                        && p.UserId == userId
                        && p.IsActive,
                cancellationToken);

        return exists;
    }

    public async Task<int> GetUnreadConversationCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        int count = await _dbContext.Messages
            .Where(m => m.SenderId != userId)
            .Where(m => _dbContext.Participants
                .Any(p => p.ConversationId == m.ConversationId
                       && p.UserId == userId
                       && p.IsActive
                       && m.SentAt > (p.LastReadAt ?? p.JoinedAt)))
            .Select(m => m.ConversationId)
            .Distinct()
            .CountAsync(cancellationToken);

        return count;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid conversationId,
        Guid userId,
        Guid? cursorMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ConversationId convId = ConversationId.Create(conversationId);

        IQueryable<Message> query = _dbContext.Messages
            .Where(m => m.ConversationId == convId)
            .Where(m => _dbContext.Participants
                .Any(p => p.ConversationId == m.ConversationId
                       && p.UserId == userId));

        if (cursorMessageId.HasValue)
        {
            MessageId cursorId = MessageId.Create(cursorMessageId.Value);

            DateTimeOffset cursorSentAt = await _dbContext.Messages
                .Where(m => m.Id == cursorId)
                .Select(m => m.SentAt)
                .FirstAsync(cancellationToken);

            query = query.Where(m => m.SentAt < cursorSentAt);
        }

        List<Message> entities = await query
            .OrderByDescending(m => m.SentAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<MessageDto> messages = entities
            .Select(m => new MessageDto(
                m.Id.Value,
                m.ConversationId.Value,
                m.SenderId,
                m.Body,
                m.SentAt.UtcDateTime,
                m.Status.ToString()))
            .ToList();

        return messages;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int offset = (page - 1) * pageSize;

        List<ConversationId> conversationIds = await _dbContext.Participants
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => p.ConversationId)
            .ToListAsync(cancellationToken);

        List<Conversation> conversations = await _dbContext.Conversations
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .Where(c => conversationIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<ConversationDto> result = conversations.Select(c =>
        {
            Message? lastMessage = c.Messages
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            Participant? userParticipant = c.Participants
                .FirstOrDefault(p => p.UserId == userId);

            int unreadCount = userParticipant is not null
                ? c.Messages.Count(m => m.SenderId != userId
                    && m.SentAt > (userParticipant.LastReadAt ?? userParticipant.JoinedAt))
                : 0;

            MessageDto? lastMessageDto = lastMessage is not null
                ? new MessageDto(
                    lastMessage.Id.Value,
                    lastMessage.ConversationId.Value,
                    lastMessage.SenderId,
                    lastMessage.Body,
                    lastMessage.SentAt.UtcDateTime,
                    lastMessage.Status.ToString())
                : null;

            string conversationType = c.IsGroup ? "Group" : "Direct";

            List<ParticipantDto> participants = c.Participants
                .Select(p => new ParticipantDto(
                    p.UserId,
                    p.JoinedAt.UtcDateTime,
                    p.LastReadAt.HasValue ? p.LastReadAt.Value.UtcDateTime : null,
                    p.IsActive))
                .ToList();

            DateTime lastActivityAt = c.UpdatedAt ?? c.CreatedAt;

            return new ConversationDto(
                c.Id.Value,
                conversationType,
                participants,
                lastMessageDto,
                unreadCount,
                lastActivityAt);
        }).ToList();

        return result;
    }
}
