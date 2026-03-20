using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class MeteringDbSeederTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{

    protected override bool UseMigrateAsync => true;

    [Fact]
    public async Task SeedAsync_CreatesMeterDefinitions()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        int meterCount = await DbContext.MeterDefinitions.CountAsync();

        meterCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task SeedAsync_CreatesExpectedMeterCodes()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        List<string> codes = await DbContext.MeterDefinitions
            .Select(m => m.Code)
            .ToListAsync();

        codes.Should().Contain("api.calls");
        codes.Should().Contain("storage.bytes");
        codes.Should().Contain("users.active");
        codes.Should().Contain("workflows.executions");
    }

    [Fact]
    public async Task SeedAsync_CreatesDefaultQuotas()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        int quotaCount = await DbContext.QuotaDefinitions
            .IgnoreQueryFilters()
            .CountAsync();

        quotaCount.Should().BeGreaterThanOrEqualTo(9); // 3 tiers * 3 meters
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);
        int countAfterFirst = await DbContext.MeterDefinitions.CountAsync();

        await MeteringDbSeeder.SeedAsync(DbContext);
        int countAfterSecond = await DbContext.MeterDefinitions.CountAsync();

        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task SeedAsync_CreatesFreeTierQuotas()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        List<string> freeMeterCodes = await DbContext.QuotaDefinitions
            .IgnoreQueryFilters()
            .Where(q => q.PlanCode == "free")
            .Select(q => q.MeterCode)
            .ToListAsync();

        freeMeterCodes.Should().Contain("api.calls");
        freeMeterCodes.Should().Contain("storage.bytes");
        freeMeterCodes.Should().Contain("users.active");
    }

    [Fact]
    public async Task SeedAsync_CreatesProTierQuotas()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        List<string> proMeterCodes = await DbContext.QuotaDefinitions
            .IgnoreQueryFilters()
            .Where(q => q.PlanCode == "pro")
            .Select(q => q.MeterCode)
            .ToListAsync();

        proMeterCodes.Should().Contain("api.calls");
        proMeterCodes.Should().Contain("storage.bytes");
        proMeterCodes.Should().Contain("users.active");
    }

    [Fact]
    public async Task SeedAsync_CreatesEnterpriseTierQuotas()
    {
        await MeteringDbSeeder.SeedAsync(DbContext);

        List<string> enterpriseMeterCodes = await DbContext.QuotaDefinitions
            .IgnoreQueryFilters()
            .Where(q => q.PlanCode == "enterprise")
            .Select(q => q.MeterCode)
            .ToListAsync();

        enterpriseMeterCodes.Should().Contain("api.calls");
        enterpriseMeterCodes.Should().Contain("storage.bytes");
        enterpriseMeterCodes.Should().Contain("users.active");
    }
}
