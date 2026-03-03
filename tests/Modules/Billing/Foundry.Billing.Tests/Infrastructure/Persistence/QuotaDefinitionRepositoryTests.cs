using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Billing.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class QuotaDefinitionRepositoryTests : DbContextIntegrationTestBase<BillingDbContext>
{
    public QuotaDefinitionRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    private QuotaDefinitionRepository CreateRepository() =>
        new QuotaDefinitionRepository(DbContext, TenantContext);

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsQuotaDefinition()
    {
        QuotaDefinitionRepository repository = CreateRepository();
        QuotaDefinition quota = QuotaDefinition.CreateTenantOverride(
            $"test.quota.{Guid.NewGuid():N}", TestTenantId, 5000, QuotaPeriod.Monthly, QuotaAction.Warn);

        repository.Add(quota);
        await repository.SaveChangesAsync();

        QuotaDefinition? result = await repository.GetByIdAsync(quota.Id);

        result.Should().NotBeNull();
        result!.Limit.Should().Be(5000);
    }

    [Fact]
    public async Task GetTenantOverrideAsync_ReturnsTenantSpecificQuota()
    {
        QuotaDefinitionRepository repository = CreateRepository();
        string meterCode = $"test.override.{Guid.NewGuid():N}";
        QuotaDefinition quota = QuotaDefinition.CreateTenantOverride(
            meterCode, TestTenantId, 10000, QuotaPeriod.Monthly, QuotaAction.Block);

        repository.Add(quota);
        await repository.SaveChangesAsync();

        QuotaDefinition? result = await repository.GetTenantOverrideAsync(meterCode);

        result.Should().NotBeNull();
        result!.MeterCode.Should().Be(meterCode);
        result.Limit.Should().Be(10000);
    }

    [Fact]
    public async Task GetTenantOverrideAsync_WhenNoOverride_ReturnsNull()
    {
        QuotaDefinitionRepository repository = CreateRepository();

        QuotaDefinition? result = await repository.GetTenantOverrideAsync("nonexistent.meter");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEffectiveQuotaAsync_WithTenantOverride_ReturnsOverride()
    {
        QuotaDefinitionRepository repository = CreateRepository();
        string meterCode = $"test.effective.{Guid.NewGuid():N}";

        QuotaDefinition tenantOverride = QuotaDefinition.CreateTenantOverride(
            meterCode, TestTenantId, 20000, QuotaPeriod.Monthly, QuotaAction.Warn);
        repository.Add(tenantOverride);
        await repository.SaveChangesAsync();

        QuotaDefinition? result = await repository.GetEffectiveQuotaAsync(meterCode, "pro");

        result.Should().NotBeNull();
        result!.Limit.Should().Be(20000);
    }

    [Fact]
    public async Task GetEffectiveQuotaAsync_WithNoPlanCode_ReturnsNullWhenNoOverride()
    {
        QuotaDefinitionRepository repository = CreateRepository();

        QuotaDefinition? result = await repository.GetEffectiveQuotaAsync("nonexistent.meter", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEffectiveQuotaAsync_WithPlanCode_ReturnsPlanDefault()
    {
        // Seed a plan-level default (system tenant)
        TenantId systemTenantId = TenantId.Create(Guid.Empty);
        await using BillingDbContext systemContext = CreateDbContextForTenant(systemTenantId, "System");
        string meterCode = $"test.plan.{Guid.NewGuid():N}";
        QuotaDefinition planDefault = QuotaDefinition.CreatePlanQuota(
            meterCode, "enterprise", 500000, QuotaPeriod.Monthly, QuotaAction.Warn);
        systemContext.QuotaDefinitions.Add(planDefault);
        await systemContext.SaveChangesAsync();

        QuotaDefinitionRepository repository = CreateRepository();

        QuotaDefinition? result = await repository.GetEffectiveQuotaAsync(meterCode, "enterprise");

        result.Should().NotBeNull();
        result!.Limit.Should().Be(500000);
        result.PlanCode.Should().Be("enterprise");
    }

    [Fact]
    public async Task GetAllForTenantAsync_ReturnsOnlyTenantQuotas()
    {
        QuotaDefinitionRepository repository = CreateRepository();
        string meterCode1 = $"test.tenant.{Guid.NewGuid():N}";
        string meterCode2 = $"test.tenant.{Guid.NewGuid():N}";

        QuotaDefinition quota1 = QuotaDefinition.CreateTenantOverride(
            meterCode1, TestTenantId, 1000, QuotaPeriod.Daily, QuotaAction.Block);
        QuotaDefinition quota2 = QuotaDefinition.CreateTenantOverride(
            meterCode2, TestTenantId, 2000, QuotaPeriod.Monthly, QuotaAction.Warn);

        repository.Add(quota1);
        repository.Add(quota2);
        await repository.SaveChangesAsync();

        IReadOnlyList<QuotaDefinition> result = await repository.GetAllForTenantAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Remove_DeletesQuotaDefinition()
    {
        QuotaDefinitionRepository repository = CreateRepository();
        QuotaDefinition quota = QuotaDefinition.CreateTenantOverride(
            $"test.remove.{Guid.NewGuid():N}", TestTenantId, 1000, QuotaPeriod.Monthly, QuotaAction.Block);

        repository.Add(quota);
        await repository.SaveChangesAsync();

        repository.Remove(quota);
        await repository.SaveChangesAsync();

        QuotaDefinition? result = await repository.GetByIdAsync(quota.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        QuotaDefinitionRepository repository = CreateRepository();

        QuotaDefinition? result = await repository.GetByIdAsync(QuotaDefinitionId.New());

        result.Should().BeNull();
    }
}
