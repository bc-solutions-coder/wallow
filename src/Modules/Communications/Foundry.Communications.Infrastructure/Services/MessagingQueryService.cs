using System.Data.Common;
using Dapper;
using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Services;

public sealed class MessagingQueryService(
    CommunicationsDbContext dbContext,
    ITenantContext tenantContext) : IMessagingQueryService
{
    public async Task<int> GetUnreadConversationCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();

        const string sql = """
            SELECT COUNT(DISTINCT m.conversation_id)
            FROM communications.messages m
            INNER JOIN communications.participants p
                ON p.conversation_id = m.conversation_id
                AND p.user_id = @UserId
            INNER JOIN communications.conversations c
                ON c.id = m.conversation_id
            WHERE c.tenant_id = @TenantId
              AND m.sender_id != @UserId
              AND m.sent_at > COALESCE(p.last_read_at, p.joined_at)
            """;

        int count = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                sql,
                new { UserId = userId, TenantId = tenantContext.TenantId.Value },
                cancellationToken: cancellationToken));

        return count;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid conversationId,
        Guid userId,
        Guid? cursorMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();

        string sql = cursorMessageId.HasValue
            ? """
              SELECT m.id AS "Id",
                     m.conversation_id AS "ConversationId",
                     m.sender_id AS "SenderId",
                     m.body AS "Body",
                     m.sent_at AS "SentAt",
                     m.status AS "Status"
              FROM communications.messages m
              INNER JOIN communications.conversations c ON c.id = m.conversation_id
              INNER JOIN communications.participants p
                  ON p.conversation_id = m.conversation_id
                  AND p.user_id = @UserId
              WHERE m.conversation_id = @ConversationId
                AND c.tenant_id = @TenantId
                AND m.sent_at < (SELECT sent_at FROM communications.messages WHERE id = @CursorMessageId)
              ORDER BY m.sent_at DESC
              LIMIT @PageSize
              """
            : """
              SELECT m.id AS "Id",
                     m.conversation_id AS "ConversationId",
                     m.sender_id AS "SenderId",
                     m.body AS "Body",
                     m.sent_at AS "SentAt",
                     m.status AS "Status"
              FROM communications.messages m
              INNER JOIN communications.conversations c ON c.id = m.conversation_id
              INNER JOIN communications.participants p
                  ON p.conversation_id = m.conversation_id
                  AND p.user_id = @UserId
              WHERE m.conversation_id = @ConversationId
                AND c.tenant_id = @TenantId
              ORDER BY m.sent_at DESC
              LIMIT @PageSize
              """;

        CommandDefinition command = new(
            sql,
            new
            {
                ConversationId = conversationId,
                UserId = userId,
                TenantId = tenantContext.TenantId.Value,
                CursorMessageId = cursorMessageId,
                PageSize = pageSize
            },
            cancellationToken: cancellationToken);

        IEnumerable<MessageDto> messages = await connection.QueryAsync<MessageDto>(command);

        return messages.ToList();
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        int offset = (page - 1) * pageSize;

        const string sql = """
            WITH user_conversations AS (
                SELECT p.conversation_id
                FROM communications.participants p
                WHERE p.user_id = @UserId
                  AND p.is_active = true
            ),
            last_messages AS (
                SELECT DISTINCT ON (m.conversation_id)
                    m.conversation_id,
                    m.id AS message_id,
                    m.sender_id,
                    m.body,
                    m.sent_at,
                    m.status
                FROM communications.messages m
                INNER JOIN user_conversations uc ON uc.conversation_id = m.conversation_id
                ORDER BY m.conversation_id, m.sent_at DESC
            ),
            unread_counts AS (
                SELECT
                    p.conversation_id,
                    COUNT(m.id) AS unread_count
                FROM communications.participants p
                INNER JOIN user_conversations uc ON uc.conversation_id = p.conversation_id
                LEFT JOIN communications.messages m
                    ON m.conversation_id = p.conversation_id
                    AND (p.last_read_at IS NULL OR m.sent_at > p.last_read_at)
                WHERE p.user_id = @UserId
                GROUP BY p.conversation_id
            )
            SELECT
                c.id AS "Id",
                c.is_group AS "Type",
                c.updated_at AS "LastActivityAt",
                lm.message_id AS "LastMessageId",
                lm.sender_id AS "LastMessageSenderId",
                lm.body AS "LastMessageBody",
                lm.sent_at AS "LastMessageSentAt",
                lm.status AS "LastMessageStatus",
                COALESCE(uc.unread_count, 0) AS "UnreadCount"
            FROM communications.conversations c
            INNER JOIN user_conversations uconv ON uconv.conversation_id = c.id
            LEFT JOIN last_messages lm ON lm.conversation_id = c.id
            LEFT JOIN unread_counts uc ON uc.conversation_id = c.id
            WHERE c.tenant_id = @TenantId
            ORDER BY COALESCE(lm.sent_at, c.updated_at, c.created_at) DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        IEnumerable<ConversationRow> rows = await connection.QueryAsync<ConversationRow>(
            new CommandDefinition(
                sql,
                new
                {
                    UserId = userId,
                    TenantId = tenantContext.TenantId.Value,
                    PageSize = pageSize,
                    Offset = offset
                },
                cancellationToken: cancellationToken));

        List<ConversationRow> rowList = rows.ToList();
        Guid[] conversationIds = rowList.Select(r => r.Id).ToArray();

        const string participantsSql = """
            SELECT conversation_id AS "ConversationId",
                   user_id AS "UserId",
                   joined_at AS "JoinedAt",
                   last_read_at AS "LastReadAt",
                   is_active AS "IsActive"
            FROM communications.participants
            WHERE conversation_id = ANY(@ConversationIds)
            """;

        IEnumerable<ParticipantRow> participantRows = await connection.QueryAsync<ParticipantRow>(
            new CommandDefinition(
                participantsSql,
                new { ConversationIds = conversationIds },
                cancellationToken: cancellationToken));

        Dictionary<Guid, List<ParticipantDto>> participantsByConversation = participantRows
            .GroupBy(p => p.ConversationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => new ParticipantDto(p.UserId, p.JoinedAt, p.LastReadAt, p.IsActive)).ToList());

        List<ConversationDto> conversations = [];

        foreach (ConversationRow row in rowList)
        {
            List<ParticipantDto> participants = participantsByConversation.GetValueOrDefault(row.Id, []);

            MessageDto? lastMessage = row.LastMessageId != Guid.Empty
                ? new MessageDto(
                    row.LastMessageId,
                    row.Id,
                    row.LastMessageSenderId,
                    row.LastMessageBody,
                    row.LastMessageSentAt,
                    row.LastMessageStatus)
                : null;

            string conversationType = row.Type ? "Group" : "Direct";

            ConversationDto dto = new(
                row.Id,
                conversationType,
                participants,
                lastMessage,
                (int)row.UnreadCount,
                row.LastActivityAt);

            conversations.Add(dto);
        }

        return conversations;
    }

    private sealed record ParticipantRow(
        Guid ConversationId,
        Guid UserId,
        DateTime JoinedAt,
        DateTime? LastReadAt,
        bool IsActive);

    private sealed record ConversationRow(
        Guid Id,
        bool Type,
        DateTime LastActivityAt,
        Guid LastMessageId,
        Guid LastMessageSenderId,
        string LastMessageBody,
        DateTime LastMessageSentAt,
        string LastMessageStatus,
        long UnreadCount);
}
