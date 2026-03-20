using System.Data.Common;
using Dapper;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class InvoiceReportService(BillingDbContext context, ITenantContext tenantContext) : IInvoiceReportService
{

    public async Task<IReadOnlyList<InvoiceReportRow>> GetInvoicesAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT
                i."InvoiceNumber",
                'User_' || i."UserId" as "CustomerName",
                i."TotalAmount_Amount" as "Amount",
                i."TotalAmount_Currency" as "Currency",
                i."Status"::text as "Status",
                i."CreatedAt" as "IssueDate",
                i."DueDate"
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" != 0
              AND i."CreatedAt" >= @From
              AND i."CreatedAt" < @To
            ORDER BY i."CreatedAt" DESC
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        IEnumerable<InvoiceReportRow> results = await connection.QueryAsync<InvoiceReportRow>(command);

        return results.AsList();
    }
}
