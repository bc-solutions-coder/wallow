using System.Data.Common;
using Dapper;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class RevenueReportService(BillingDbContext context, ITenantContext tenantContext) : IRevenueReportService
{

    public async Task<IReadOnlyList<RevenueReportRow>> GetRevenueAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT
                TO_CHAR(@From, 'YYYY-MM-DD') || ' to ' || TO_CHAR(@To, 'YYYY-MM-DD') AS Period,
                COALESCE(SUM(CASE WHEN i."Status" = 2 THEN i."TotalAmount_Amount" ELSE 0 END), 0) AS GrossRevenue,
                COALESCE(SUM(CASE WHEN i."Status" = 2 THEN i."TotalAmount_Amount" ELSE 0 END), 0) AS NetRevenue,
                COALESCE(SUM(CASE WHEN p."Status" = 3 THEN p."Amount_Amount" ELSE 0 END), 0) AS Refunds,
                COALESCE(MAX(i."TotalAmount_Currency"), 'USD') AS Currency,
                COUNT(DISTINCT i."Id") AS InvoiceCount,
                COUNT(DISTINCT p."Id") AS PaymentCount
            FROM billing."Invoices" i
            LEFT JOIN billing."Payments" p ON p."InvoiceId" = i."Id" AND p."TenantId" = @TenantId
            WHERE i."TenantId" = @TenantId
              AND i."CreatedAt" >= @From
              AND i."CreatedAt" < @To
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        IEnumerable<RevenueReportRow> results = await connection.QueryAsync<RevenueReportRow>(command);

        return results.AsList();
    }
}
