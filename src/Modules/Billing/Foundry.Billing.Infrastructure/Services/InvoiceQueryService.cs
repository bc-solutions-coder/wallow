using System.Data.Common;
using Dapper;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class InvoiceQueryService : IInvoiceQueryService
{
    private readonly BillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceQueryService(BillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

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
            new { TenantId = _tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        decimal result = await connection.QuerySingleAsync<decimal>(command);

        return result;
    }

    public async Task<int> GetCountAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

        const string sql = """
            SELECT COUNT(*)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."CreatedAt" >= @From
              AND i."CreatedAt" < @To
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = _tenantContext.TenantId.Value, From = from, To = to },
            cancellationToken: ct);

        int result = await connection.QuerySingleAsync<int>(command);

        return result;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

        const string sql = """
            SELECT COUNT(*)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" = 1
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = _tenantContext.TenantId.Value },
            cancellationToken: ct);

        int result = await connection.QuerySingleAsync<int>(command);

        return result;
    }

    public async Task<decimal> GetOutstandingAmountAsync(CancellationToken ct = default)
    {
        DbConnection connection = _context.Database.GetDbConnection();

        const string sql = """
            SELECT COALESCE(SUM(i."TotalAmount_Amount"), 0)
            FROM billing."Invoices" i
            WHERE i."TenantId" = @TenantId
              AND i."Status" IN (1, 3)
            """;

        CommandDefinition command = new(
            sql,
            new { TenantId = _tenantContext.TenantId.Value },
            cancellationToken: ct);

        decimal result = await connection.QuerySingleAsync<decimal>(command);

        return result;
    }
}
