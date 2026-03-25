using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Contracts.Metering;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Tests.Infrastructure.Services;

public class UsageReportServiceTests
{
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public void Constructor_CreatesInstance()
    {
        using BillingDbContext dbContext = CreateDbContext();

        UsageReportService service = new(dbContext);

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsGroupedUsageByMeterCode()
    {
        await using BillingDbContext dbContext = CreateDbContext();
        DateTime from = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodStart = new(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);

        MeterDefinition meter = MeterDefinition.Create(
            "api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        dbContext.MeterDefinitions.Add(meter);

        UsageRecord record1 = UsageRecord.Create(_tenantId, "api.calls", periodStart, periodEnd, 100, TimeProvider.System);
        UsageRecord record2 = UsageRecord.Create(_tenantId, "api.calls", periodStart, periodEnd, 50, TimeProvider.System);
        dbContext.UsageRecords.AddRange(record1, record2);
        await dbContext.SaveChangesAsync();

        UsageReportService service = new(dbContext);

        IReadOnlyList<UsageReportRow> results = await service.GetUsageAsync(
            _tenantId.Value, from, to);

        results.Should().HaveCount(1);
        results[0].Metric.Should().Be("API Calls");
        results[0].Quantity.Should().Be(150);
        results[0].Unit.Should().Be("requests");
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsEmptyWhenNoRecords()
    {
        await using BillingDbContext dbContext = CreateDbContext();
        DateTime from = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        UsageReportService service = new(dbContext);

        IReadOnlyList<UsageReportRow> results = await service.GetUsageAsync(
            _tenantId.Value, from, to);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsageAsync_FiltersOutRecordsOutsideDateRange()
    {
        await using BillingDbContext dbContext = CreateDbContext();
        DateTime from = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        MeterDefinition meter = MeterDefinition.Create(
            "storage.bytes", "Storage", "bytes", MeterAggregation.Sum, true);
        dbContext.MeterDefinitions.Add(meter);

        UsageRecord inside = UsageRecord.Create(_tenantId, "storage.bytes", new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc), 500, TimeProvider.System);
        UsageRecord outside = UsageRecord.Create(_tenantId, "storage.bytes", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc), 300, TimeProvider.System);
        dbContext.UsageRecords.AddRange(inside, outside);
        await dbContext.SaveChangesAsync();

        UsageReportService service = new(dbContext);

        IReadOnlyList<UsageReportRow> results = await service.GetUsageAsync(
            _tenantId.Value, from, to);

        results.Should().HaveCount(1);
        results[0].Quantity.Should().Be(500);
    }

    [Fact]
    public async Task GetUsageAsync_RespectsTenantIsolation()
    {
        await using BillingDbContext dbContext = CreateDbContext();
        DateTime from = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        MeterDefinition meter = MeterDefinition.Create(
            "api.requests", "API Requests", "requests", MeterAggregation.Sum, true);
        dbContext.MeterDefinitions.Add(meter);

        UsageRecord record = UsageRecord.Create(_tenantId, "api.requests", new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc), 200, TimeProvider.System);
        dbContext.UsageRecords.Add(record);
        await dbContext.SaveChangesAsync();

        Guid otherTenantId = Guid.NewGuid();

        UsageReportService service = new(dbContext);

        IReadOnlyList<UsageReportRow> results = await service.GetUsageAsync(
            otherTenantId, from, to);

        results.Should().BeEmpty();
    }

    private BillingDbContext CreateDbContext()
    {
        DbContextOptions<BillingDbContext> options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);

        BillingDbContext dbContext = new(options);
        dbContext.SetTenant(_tenantId);
        return dbContext;
    }
}
