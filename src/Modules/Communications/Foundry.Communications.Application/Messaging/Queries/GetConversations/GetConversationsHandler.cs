using System.Data;
using Dapper;
using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Queries.GetConversations;

public sealed class GetConversationsHandler(IDbConnection dbConnection, ITenantContext tenantContext)
{
    public async Task<Result<IReadOnlyList<ConversationDto>>> Handle(
        GetConversationsQuery query,
        CancellationToken cancellationToken)
    {
        int offset = (query.Page - 1) * query.PageSize;

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
                c.id,
                c.is_group AS type,
                c.updated_at AS last_activity_at,
                lm.message_id AS last_message_id,
                lm.sender_id AS last_message_sender_id,
                lm.body AS last_message_body,
                lm.sent_at AS last_message_sent_at,
                lm.status AS last_message_status,
                COALESCE(uc.unread_count, 0) AS unread_count
            FROM communications.conversations c
            INNER JOIN user_conversations uconv ON uconv.conversation_id = c.id
            LEFT JOIN last_messages lm ON lm.conversation_id = c.id
            LEFT JOIN unread_counts uc ON uc.conversation_id = c.id
            WHERE c.tenant_id = @TenantId
            ORDER BY COALESCE(lm.sent_at, c.updated_at, c.created_at) DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        IEnumerable<ConversationRow> rows = await dbConnection.QueryAsync<ConversationRow>(
            new CommandDefinition(
                sql,
                new
                {
                    UserId = query.UserId,
                    TenantId = tenantContext.TenantId.Value,
                    query.PageSize,
                    Offset = offset
                },
                cancellationToken: cancellationToken));

        // Load participants for each conversation
        List<ConversationDto> conversations = [];

        foreach (ConversationRow row in rows)
        {
            const string participantsSql = """
                SELECT user_id, joined_at, last_read_at, is_active
                FROM communications.participants
                WHERE conversation_id = @ConversationId
                """;

            IEnumerable<ParticipantDto> participants = await dbConnection.QueryAsync<ParticipantDto>(
                new CommandDefinition(
                    participantsSql,
                    new { ConversationId = row.Id },
                    cancellationToken: cancellationToken));

            MessageDto? lastMessage = row.LastMessageId.HasValue
                ? new MessageDto(
                    row.LastMessageId.Value,
                    row.Id,
                    row.LastMessageSenderId!.Value,
                    row.LastMessageBody!,
                    row.LastMessageSentAt!.Value,
                    row.LastMessageStatus!)
                : null;

            string conversationType = row.Type ? "Group" : "Direct";

            ConversationDto dto = new(
                row.Id,
                conversationType,
                participants.ToList(),
                lastMessage,
                row.UnreadCount,
                row.LastActivityAt ?? DateTimeOffset.MinValue);

            conversations.Add(dto);
        }

        return Result.Success<IReadOnlyList<ConversationDto>>(conversations);
    }

    private sealed record ConversationRow(
        Guid Id,
        bool Type,
        DateTimeOffset? LastActivityAt,
        Guid? LastMessageId,
        Guid? LastMessageSenderId,
        string? LastMessageBody,
        DateTimeOffset? LastMessageSentAt,
        string? LastMessageStatus,
        int UnreadCount);
}
