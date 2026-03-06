using System.Data.Common;
using Dapper;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class InvoiceReportService : IInvoiceReportService
{
    private readonly BillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceReportService(BillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<InvoiceReportRow>> GetInvoicesAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

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
            new { TenantId = _tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        IEnumerable<InvoiceReportRow> results = await connection.QueryAsync<InvoiceReportRow>(command);

        return results.AsList();
    }
}
