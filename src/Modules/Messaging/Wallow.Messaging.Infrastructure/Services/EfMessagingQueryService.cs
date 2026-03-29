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
                       && p.UserId == userId
                       && p.IsActive));

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

        var projections = await _dbContext.Conversations
            .Where(c => conversationIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.IsGroup,
                c.CreatedAt,
                c.UpdatedAt,
                Participants = _dbContext.Participants
                    .Where(p => p.ConversationId == c.Id)
                    .Select(p => new
                    {
                        p.UserId,
                        p.JoinedAt,
                        p.LastReadAt,
                        p.IsActive
                    })
                    .ToList(),
                LastMessage = _dbContext.Messages
                    .Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.ConversationId,
                        m.SenderId,
                        m.Body,
                        m.SentAt,
                        m.Status
                    })
                    .FirstOrDefault(),
                UnreadCount = _dbContext.Participants
                    .Where(p => p.ConversationId == c.Id && p.UserId == userId)
                    .Select(p => _dbContext.Messages
                        .Count(m => m.ConversationId == c.Id
                                 && m.SenderId != userId
                                 && m.SentAt > (p.LastReadAt ?? p.JoinedAt)))
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        List<ConversationDto> result = projections.Select(c => new ConversationDto(
            c.Id.Value,
            c.IsGroup ? "Group" : "Direct",
            c.Participants.Select(p => new ParticipantDto(
                p.UserId,
                p.JoinedAt.UtcDateTime,
                p.LastReadAt?.UtcDateTime,
                p.IsActive)).ToList(),
            c.LastMessage is not null
                ? new MessageDto(
                    c.LastMessage.Id.Value,
                    c.LastMessage.ConversationId.Value,
                    c.LastMessage.SenderId,
                    c.LastMessage.Body,
                    c.LastMessage.SentAt.UtcDateTime,
                    c.LastMessage.Status.ToString())
                : null,
            c.UnreadCount,
            c.UpdatedAt ?? c.CreatedAt)).ToList();

        return result;
    }
}
