using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Integration.Metering;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class UsageRecordRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    private UsageRecordRepository _repository = null!;

    protected override bool UseMigrateAsync => true;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new UsageRecordRepository(DbContext, TenantContext);
    }

    [Fact]
    public async Task Add_PersistsUsageRecord()
    {
        DateTime periodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        UsageRecord record = UsageRecord.Create(TestTenantId, "api_calls", periodStart, periodEnd, 1500, TimeProvider.System);

        _repository.Add(record);
        await _repository.SaveChangesAsync();

        UsageRecord? retrieved = await _repository.GetByIdAsync(record.Id);

        retrieved.Should().NotBeNull();
        retrieved.MeterCode.Should().Be("api_calls");
        retrieved.Value.Should().Be(1500);
        retrieved.PeriodStart.Should().Be(periodStart);
        retrieved.PeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsRecordsInDateRange()
    {
        UsageRecord jan = UsageRecord.Create(TestTenantId, "storage_gb", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 100, TimeProvider.System);
        UsageRecord feb = UsageRecord.Create(TestTenantId, "storage_gb", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), 150, TimeProvider.System);
        UsageRecord mar = UsageRecord.Create(TestTenantId, "storage_gb", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), 200, TimeProvider.System);

        _repository.Add(jan);
        _repository.Add(feb);
        _repository.Add(mar);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<UsageRecord> results = await _repository.GetHistoryAsync(
            "storage_gb",
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));

        results.Should().HaveCount(1);
        results[0].Value.Should().Be(150);
    }

    [Fact]
    public async Task GetHistoryAsync_FiltersByMeterCode()
    {
        UsageRecord apiCalls = UsageRecord.Create(TestTenantId, "api_calls", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 500, TimeProvider.System);
        UsageRecord storage = UsageRecord.Create(TestTenantId, "storage_gb", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 100, TimeProvider.System);

        _repository.Add(apiCalls);
        _repository.Add(storage);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<UsageRecord> results = await _repository.GetHistoryAsync(
            "api_calls",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        results.Should().HaveCount(1);
        results[0].MeterCode.Should().Be("api_calls");
        results[0].Value.Should().Be(500);
    }

    [Fact]
    public async Task GetForPeriodAsync_FindsExactPeriod()
    {
        DateTime periodStart = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        UsageRecord record = UsageRecord.Create(TestTenantId, "bandwidth_gb", periodStart, periodEnd, 750, TimeProvider.System);
        _repository.Add(record);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        UsageRecord? retrieved = await _repository.GetForPeriodAsync("bandwidth_gb", periodStart, periodEnd);

        retrieved.Should().NotBeNull();
        retrieved.Value.Should().Be(750);
    }

    [Fact]
    public async Task GetForPeriodAsync_ReturnsNullWhenNotFound()
    {
        UsageRecord? retrieved = await _repository.GetForPeriodAsync(
            "nonexistent",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        UsageRecord record = UsageRecord.Create(TestTenantId, "compute_hours", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 50, TimeProvider.System);
        _repository.Add(record);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        UsageRecord? retrieved = await _repository.GetByIdAsync(record.Id);
        retrieved!.AddValue(25, TimeProvider.System);

        _repository.Update(retrieved);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        UsageRecord? updated = await _repository.GetByIdAsync(record.Id);
        updated!.Value.Should().Be(75);
    }

    [Fact]
    public async Task RespectsTenantIsolation()
    {
        UsageRecord record1 = UsageRecord.Create(TestTenantId, "api_calls", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 100, TimeProvider.System);
        _repository.Add(record1);
        await _repository.SaveChangesAsync();

        TenantId otherTenantId = TenantId.New();
        TenantContext otherTenantContext = new();
        otherTenantContext.SetTenant(otherTenantId, "OtherTenant");
        await using BillingDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);
        UsageRecordRepository otherRepository = new(otherDbContext, otherTenantContext);

        UsageRecord record2 = UsageRecord.Create(otherTenantId, "api_calls", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 200, TimeProvider.System);
        otherRepository.Add(record2);
        await otherDbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        otherDbContext.ChangeTracker.Clear();

        IReadOnlyList<UsageRecord> tenant1Results = await _repository.GetHistoryAsync("api_calls", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
        IReadOnlyList<UsageRecord> tenant2Results = await otherRepository.GetHistoryAsync("api_calls", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        tenant1Results.Should().HaveCount(1);
        tenant1Results[0].Value.Should().Be(100);

        tenant2Results.Should().HaveCount(1);
        tenant2Results[0].Value.Should().Be(200);
    }
}
