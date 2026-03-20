using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class MeterDefinitionRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private MeterDefinitionRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsMeterDefinition()
    {
        MeterDefinitionRepository repository = CreateRepository();
        MeterDefinition meter = MeterDefinition.Create(
            $"test.meter.{Guid.NewGuid():N}",
            "Test Meter",
            "requests",
            MeterAggregation.Sum,
            true);

        repository.Add(meter);
        await repository.SaveChangesAsync();

        MeterDefinition? result = await repository.GetByIdAsync(meter.Id);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Test Meter");
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsMatchingMeter()
    {
        MeterDefinitionRepository repository = CreateRepository();
        string code = $"test.code.{Guid.NewGuid():N}";
        MeterDefinition meter = MeterDefinition.Create(
            code, "Code Test Meter", "bytes", MeterAggregation.Max, true);

        repository.Add(meter);
        await repository.SaveChangesAsync();

        MeterDefinition? result = await repository.GetByCodeAsync(code);

        result.Should().NotBeNull();
        result.Code.Should().Be(code);
        result.Unit.Should().Be("bytes");
    }

    [Fact]
    public async Task GetByCodeAsync_WhenNotExists_ReturnsNull()
    {
        MeterDefinitionRepository repository = CreateRepository();

        MeterDefinition? result = await repository.GetByCodeAsync("nonexistent.meter");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllMeters()
    {
        MeterDefinitionRepository repository = CreateRepository();
        string code1 = $"a.test.all.{Guid.NewGuid():N}";
        string code2 = $"b.test.all.{Guid.NewGuid():N}";
        MeterDefinition meter1 = MeterDefinition.Create(code1, "Meter A", "units", MeterAggregation.Sum, true);
        MeterDefinition meter2 = MeterDefinition.Create(code2, "Meter B", "units", MeterAggregation.Sum, true);

        repository.Add(meter1);
        repository.Add(meter2);
        await repository.SaveChangesAsync();

        IReadOnlyList<MeterDefinition> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        MeterDefinitionRepository repository = CreateRepository();

        MeterDefinition? result = await repository.GetByIdAsync(MeterDefinitionId.New());

        result.Should().BeNull();
    }
}
