using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Persistence.Repositories;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Configuration.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class FeatureFlagRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<ConfigurationDbContext>(fixture)
{

    protected override bool UseMigrateAsync => true;

    private FeatureFlagRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsFlag()
    {
        FeatureFlagRepository repository = CreateRepository();
        FeatureFlag flag = FeatureFlag.CreateBoolean($"test_{Guid.NewGuid():N}", "Test Feature", true, TimeProvider.System);

        await repository.AddAsync(flag);

        FeatureFlag? result = await repository.GetByIdAsync(flag.Id);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test Feature");
        result.DefaultEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        FeatureFlagRepository repository = CreateRepository();

        FeatureFlag? result = await repository.GetByIdAsync(FeatureFlagId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsMatchingFlag()
    {
        FeatureFlagRepository repository = CreateRepository();
        string key = $"key_{Guid.NewGuid():N}";
        FeatureFlag flag = FeatureFlag.CreateBoolean(key, "Keyed Feature", false, TimeProvider.System);

        await repository.AddAsync(flag);

        FeatureFlag? result = await repository.GetByKeyAsync(key);

        result.Should().NotBeNull();
        result.Key.Should().Be(key);
    }

    [Fact]
    public async Task GetByKeyAsync_WhenNotExists_ReturnsNull()
    {
        FeatureFlagRepository repository = CreateRepository();

        FeatureFlag? result = await repository.GetByKeyAsync("nonexistent_key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_IncludesOverrides()
    {
        FeatureFlagRepository repository = CreateRepository();
        string key = $"key_{Guid.NewGuid():N}";
        FeatureFlag flag = FeatureFlag.CreateBoolean(key, "Flag with Override", true, TimeProvider.System);
        await repository.AddAsync(flag);

        // Add an override via a separate path
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), false, TimeProvider.System);
        DbContext.FeatureFlagOverrides.Add(over);
        await DbContext.SaveChangesAsync();

        FeatureFlag? result = await repository.GetByKeyAsync(key);

        result.Should().NotBeNull();
        result.Overrides.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllFlagsOrderedByKey()
    {
        FeatureFlagRepository repository = CreateRepository();
        string key1 = $"aaa_{Guid.NewGuid():N}";
        string key2 = $"zzz_{Guid.NewGuid():N}";
        FeatureFlag flag1 = FeatureFlag.CreateBoolean(key1, "First", true, TimeProvider.System);
        FeatureFlag flag2 = FeatureFlag.CreateBoolean(key2, "Second", false, TimeProvider.System);

        await repository.AddAsync(flag1);
        await repository.AddAsync(flag2);

        IReadOnlyList<FeatureFlag> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
        List<string> keys = result.Select(f => f.Key).ToList();
        int idx1 = keys.IndexOf(key1);
        int idx2 = keys.IndexOf(key2);
        idx1.Should().BeLessThan(idx2, "flags should be ordered by key");
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingFlag()
    {
        FeatureFlagRepository repository = CreateRepository();
        string key = $"upd_{Guid.NewGuid():N}";
        FeatureFlag flag = FeatureFlag.CreateBoolean(key, "Original", true, TimeProvider.System);
        await repository.AddAsync(flag);

        flag.Update("Updated Name", "Updated desc", false, TimeProvider.System);
        await repository.UpdateAsync(flag);

        FeatureFlag? result = await repository.GetByIdAsync(flag.Id);

        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated desc");
        result.DefaultEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesFlag()
    {
        FeatureFlagRepository repository = CreateRepository();
        string key = $"del_{Guid.NewGuid():N}";
        FeatureFlag flag = FeatureFlag.CreateBoolean(key, "To Delete", true, TimeProvider.System);
        await repository.AddAsync(flag);

        await repository.DeleteAsync(flag);

        FeatureFlag? result = await repository.GetByIdAsync(flag.Id);

        result.Should().BeNull();
    }
}
