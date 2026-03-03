using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Persistence.Repositories;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Configuration.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class FeatureFlagOverrideRepositoryTests : DbContextIntegrationTestBase<ConfigurationDbContext>
{
    public FeatureFlagOverrideRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    private FeatureFlagOverrideRepository CreateOverrideRepository() => new(DbContext, TimeProvider.System);

    private async Task<FeatureFlag> CreateAndSaveFlagAsync(string? key = null)
    {
        key ??= $"flag_{Guid.NewGuid():N}";
        FeatureFlag flag = FeatureFlag.CreateBoolean(key, "Test Flag", true, TimeProvider.System);
        DbContext.FeatureFlags.Add(flag);
        await DbContext.SaveChangesAsync();
        return flag;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsOverride()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, true, TimeProvider.System);

        await repository.AddAsync(over);

        FeatureFlagOverride? result = await repository.GetByIdAsync(over.Id);

        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();

        FeatureFlagOverride? result = await repository.GetByIdAsync(FeatureFlagOverrideId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExcludesExpiredOverrides()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();
        FeatureFlagOverride expired = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System, expiresAt: DateTime.UtcNow.AddMinutes(-10));

        await repository.AddAsync(expired);

        FeatureFlagOverride? result = await repository.GetByIdAsync(expired.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOverridesForFlagAsync_ReturnsActiveOverrides()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();
        FeatureFlagOverride activeOver = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System);
        FeatureFlagOverride expiredOver = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), false, TimeProvider.System, expiresAt: DateTime.UtcNow.AddMinutes(-10));

        await repository.AddAsync(activeOver);
        await repository.AddAsync(expiredOver);

        IReadOnlyList<FeatureFlagOverride> result = await repository.GetOverridesForFlagAsync(flag.Id);

        result.Should().Contain(o => o.Id == activeOver.Id);
        result.Should().NotContain(o => o.Id == expiredOver.Id);
    }

    [Fact]
    public async Task GetOverrideAsync_ReturnsTenantUserMatch()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenantUser(flag.Id, tenantId, userId, true, TimeProvider.System);

        await repository.AddAsync(over);

        FeatureFlagOverride? result = await repository.GetOverrideAsync(flag.Id, tenantId, userId);

        result.Should().NotBeNull();
        result.Id.Should().Be(over.Id);
    }

    [Fact]
    public async Task GetOverrideAsync_WhenNoMatch_ReturnsNull()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();

        FeatureFlagOverride? result = await repository.GetOverrideAsync(flag.Id, Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesOverride()
    {
        FeatureFlagOverrideRepository repository = CreateOverrideRepository();
        FeatureFlag flag = await CreateAndSaveFlagAsync();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System);
        await repository.AddAsync(over);

        await repository.DeleteAsync(over);

        FeatureFlagOverride? result = await repository.GetByIdAsync(over.Id);

        result.Should().BeNull();
    }
}
