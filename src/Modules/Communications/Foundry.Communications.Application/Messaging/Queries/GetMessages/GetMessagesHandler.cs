using System.Data.Common;
using Dapper;
using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Queries.GetMessages;

public sealed class GetMessagesHandler(
    DbConnection dbConnection,
    ITenantContext tenantContext)
{
    public async Task<Result<IReadOnlyList<MessageDto>>> Handle(
        GetMessagesQuery query,
        CancellationToken cancellationToken)
    {
        string sql = query.CursorMessageId.HasValue
            ? """
              SELECT id, conversation_id, sender_id, body, sent_at, status
              FROM communications.messages
              WHERE conversation_id = @ConversationId
                AND tenant_id = @TenantId
                AND sent_at < (SELECT sent_at FROM communications.messages WHERE id = @CursorMessageId)
              ORDER BY sent_at DESC
              LIMIT @PageSize
              """
            : """
              SELECT id, conversation_id, sender_id, body, sent_at, status
              FROM communications.messages
              WHERE conversation_id = @ConversationId
                AND tenant_id = @TenantId
              ORDER BY sent_at DESC
              LIMIT @PageSize
              """;

        CommandDefinition command = new(
            sql,
            new
            {
                query.ConversationId,
                TenantId = tenantContext.TenantId.Value,
                CursorMessageId = query.CursorMessageId,
                query.PageSize
            },
            cancellationToken: cancellationToken);

        IEnumerable<MessageDto> messages = await dbConnection.QueryAsync<MessageDto>(command);

        return Result.Success<IReadOnlyList<MessageDto>>(messages.ToList());
    }
}
