using System.Data.Common;
using Dapper;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class PaymentReportService : IPaymentReportService
{
    private readonly BillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public PaymentReportService(BillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PaymentReportRow>> GetPaymentsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

        const string sql = """
            SELECT
                p."Id" as "PaymentId",
                i."InvoiceNumber",
                p."Amount_Amount" as "Amount",
                p."Amount_Currency" as "Currency",
                p."Method"::text as "Method",
                p."Status"::text as "Status",
                p."CompletedAt" as "PaymentDate"
            FROM billing."Payments" p
            JOIN billing."Invoices" i ON i."Id" = p."InvoiceId"
            WHERE p."TenantId" = @TenantId
              AND p."CreatedAt" >= @From
              AND p."CreatedAt" < @To
            ORDER BY p."CreatedAt" DESC
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = _tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        IEnumerable<PaymentReportRow> results = await connection.QueryAsync<PaymentReportRow>(command);

        return results.AsList();
    }
}
