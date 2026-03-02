using System.Data.Common;
using Dapper;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Messaging.Queries.GetUnreadConversationCount;

public sealed class GetUnreadConversationCountHandler(
    DbConnection dbConnection,
    ITenantContext tenantContext)
{
    public async Task<Result<int>> Handle(
        GetUnreadConversationCountQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(DISTINCT m.conversation_id)
            FROM communications.messages m
            INNER JOIN communications.participants p
                ON p.conversation_id = m.conversation_id
                AND p.user_id = @UserId
            WHERE p.tenant_id = @TenantId
              AND m.sender_id != @UserId
              AND m.sent_at > COALESCE(p.last_read_at, p.joined_at)
            """;

        int count = await dbConnection.QuerySingleAsync<int>(
            sql,
            new
            {
                query.UserId,
                TenantId = tenantContext.TenantId.Value
            });

        return Result.Success(count);
    }
}
