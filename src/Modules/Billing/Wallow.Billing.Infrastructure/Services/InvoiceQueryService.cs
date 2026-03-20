using System.Data.Common;
using Dapper;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class InvoiceQueryService(BillingDbContext context, ITenantContext tenantContext) : IInvoiceQueryService
{

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT COALESCE(SUM(i."TotalAmount_Amount"), 0)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" = 2
              AND i."PaidAt" >= @From
              AND i."PaidAt" < @To
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        decimal result = await connection.QuerySingleAsync<decimal>(command);

        return result;
    }

    public async Task<int> GetCountAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT COUNT(*)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."CreatedAt" >= @From
              AND i."CreatedAt" < @To
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        int result = await connection.QuerySingleAsync<int>(command);

        return result;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT COUNT(*)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" = 1
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value },
            cancellationToken: ct);

        int result = await connection.QuerySingleAsync<int>(command);

        return result;
    }

    public async Task<decimal> GetOutstandingAmountAsync(CancellationToken ct = default)
    {
        DbConnection connection = context.Database.GetDbConnection();

        const string sql = """
            SELECT COALESCE(SUM(i."TotalAmount_Amount"), 0)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" IN (1, 3)
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = tenantContext.TenantId.Value },
            cancellationToken: ct);

        decimal result = await connection.QuerySingleAsync<decimal>(command);

        return result;
    }
}
