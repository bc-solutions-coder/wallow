using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.ValueObjects;
using Foundry.Configuration.Infrastructure.Services;
using Wolverine;

namespace Foundry.Configuration.Tests.Application.Services;

public class FeatureFlagServiceTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly FeatureFlagService _sut;

    public FeatureFlagServiceTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new(_repository, _messageBus, TimeProvider.System);
    }

    #region IsEnabledAsync - Boolean flags

    [Fact]
    public async Task IsEnabledAsync_FlagNotFound_ReturnsFalse()
    {
        _repository.GetByKeyAsync("missing-flag", Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        bool result = await _sut.IsEnabledAsync("missing-flag", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_BooleanFlagEnabled_ReturnsTrue()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: true, TimeProvider.System);
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_BooleanFlagDisabled_ReturnsFalse()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: false, TimeProvider.System);
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", Guid.NewGuid());

        result.Should().BeFalse();
    }

    #endregion

    #region IsEnabledAsync - Override resolution

    [Fact]
    public async Task IsEnabledAsync_WithTenantOverrideEnabled_ReturnsTrue()
    {
        Guid tenantId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: false, TimeProvider.System);
        AddOverride(flag, FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, isEnabled: true, TimeProvider.System));
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", tenantId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WithUserOverride_TakesPrecedenceOverTenantOverride()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: false, TimeProvider.System);
        AddOverride(flag, FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, isEnabled: true, TimeProvider.System));
        AddOverride(flag, FeatureFlagOverride.CreateForUser(flag.Id, userId, isEnabled: false, TimeProvider.System));
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", tenantId, userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WithUserTenantOverride_TakesPrecedenceOverUserOnly()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: false, TimeProvider.System);
        AddOverride(flag, FeatureFlagOverride.CreateForUser(flag.Id, userId, isEnabled: false, TimeProvider.System));
        AddOverride(flag, FeatureFlagOverride.CreateForTenantUser(flag.Id, tenantId, userId, isEnabled: true, TimeProvider.System));
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", tenantId, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WithExpiredOverride_IgnoresOverride()
    {
        Guid tenantId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: false, TimeProvider.System);
        AddOverride(flag, FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, isEnabled: true, TimeProvider.System, expiresAt: DateTime.UtcNow.AddDays(-1)));
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("feature-x", tenantId);

        result.Should().BeFalse();
    }

    #endregion

    #region IsEnabledAsync - Percentage flags

    [Fact]
    public async Task IsEnabledAsync_PercentageFlag100_ReturnsTrue()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("pct-flag", "PCT Flag", 100, TimeProvider.System);
        _repository.GetByKeyAsync("pct-flag", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("pct-flag", Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageFlag0_ReturnsFalse()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("pct-flag", "PCT Flag", 0, TimeProvider.System);
        _repository.GetByKeyAsync("pct-flag", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _sut.IsEnabledAsync("pct-flag", Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageFlag_ConsistentForSameUser()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("pct-flag", "PCT Flag", 50, TimeProvider.System);
        _repository.GetByKeyAsync("pct-flag", Arg.Any<CancellationToken>()).Returns(flag);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        bool result1 = await _sut.IsEnabledAsync("pct-flag", tenantId, userId);
        bool result2 = await _sut.IsEnabledAsync("pct-flag", tenantId, userId);

        result1.Should().Be(result2);
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageFlagNoUser_ReturnsTrueWhenPercentagePositive()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("pct-flag", "PCT Flag", 50, TimeProvider.System);
        _repository.GetByKeyAsync("pct-flag", Arg.Any<CancellationToken>()).Returns(flag);

        // With no userId, percentage > 0 returns true
        bool result = await _sut.IsEnabledAsync("pct-flag", Guid.NewGuid(), userId: null);

        result.Should().BeTrue();
    }

    #endregion

    #region GetVariantAsync

    [Fact]
    public async Task GetVariantAsync_FlagNotFound_ReturnsNull()
    {
        _repository.GetByKeyAsync("missing", Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        string? result = await _sut.GetVariantAsync("missing", Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVariantAsync_WithOverrideVariant_ReturnsOverrideVariant()
    {
        Guid tenantId = Guid.NewGuid();
        List<VariantWeight> variants = [new("A", 50),
            new("B", 50)];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab-test", "AB Test", variants, "A", TimeProvider.System);
        AddOverride(flag, FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, isEnabled: null, TimeProvider.System, variant: "B"));
        _repository.GetByKeyAsync("ab-test", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _sut.GetVariantAsync("ab-test", tenantId);

        result.Should().Be("B");
    }

    [Fact]
    public async Task GetVariantAsync_VariantFlag_ReturnsOneOfDefinedVariants()
    {
        List<VariantWeight> variants = [new("A", 50),
            new("B", 50)];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab-test", "AB Test", variants, "A", TimeProvider.System);
        _repository.GetByKeyAsync("ab-test", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _sut.GetVariantAsync("ab-test", Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOneOf("A", "B");
    }

    [Fact]
    public async Task GetVariantAsync_VariantFlag_ConsistentForSameUser()
    {
        List<VariantWeight> variants = [new("A", 50),
            new("B", 50)];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab-test", "AB Test", variants, "A", TimeProvider.System);
        _repository.GetByKeyAsync("ab-test", Arg.Any<CancellationToken>()).Returns(flag);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        string? result1 = await _sut.GetVariantAsync("ab-test", tenantId, userId);
        string? result2 = await _sut.GetVariantAsync("ab-test", tenantId, userId);

        result1.Should().Be(result2);
    }

    [Fact]
    public async Task GetVariantAsync_NonVariantFlag_ReturnsDefaultVariant()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("bool-flag", "Bool Flag", defaultEnabled: true, TimeProvider.System);
        _repository.GetByKeyAsync("bool-flag", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _sut.GetVariantAsync("bool-flag", Guid.NewGuid());

        // Boolean flags have no variants, should return DefaultVariant (null)
        result.Should().BeNull();
    }

    #endregion

    #region GetAllFlagsAsync

    [Fact]
    public async Task GetAllFlagsAsync_ReturnsBooleanAndVariantFlags()
    {
        Guid tenantId = Guid.NewGuid();
        List<VariantWeight> variants = [new("A", 50),
            new("B", 50)];
        FeatureFlag boolFlag = FeatureFlag.CreateBoolean("bool-flag", "Bool", defaultEnabled: true, TimeProvider.System);
        FeatureFlag variantFlag = FeatureFlag.CreateVariant("var-flag", "Var", variants, "A", TimeProvider.System);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { boolFlag, variantFlag });
        _repository.GetByKeyAsync("bool-flag", Arg.Any<CancellationToken>()).Returns(boolFlag);
        _repository.GetByKeyAsync("var-flag", Arg.Any<CancellationToken>()).Returns(variantFlag);

        Dictionary<string, object> result = await _sut.GetAllFlagsAsync(tenantId);

        result.Should().ContainKey("bool-flag");
        result["bool-flag"].Should().Be(true);
        result.Should().ContainKey("var-flag");
        result["var-flag"].Should().BeOneOf("A", "B");
    }

    [Fact]
    public async Task GetAllFlagsAsync_EmptyList_ReturnsEmptyDictionary()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag>());

        Dictionary<string, object> result = await _sut.GetAllFlagsAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    #endregion

    #region Event publishing

    [Fact]
    public async Task IsEnabledAsync_PublishesEvaluationEvent()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature-x", "Feature X", defaultEnabled: true, TimeProvider.System);
        _repository.GetByKeyAsync("feature-x", Arg.Any<CancellationToken>()).Returns(flag);

        await _sut.IsEnabledAsync("feature-x", Guid.NewGuid());

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task GetVariantAsync_PublishesEvaluationEvent()
    {
        List<VariantWeight> variants = [new("A", 100)];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab-test", "AB Test", variants, "A", TimeProvider.System);
        _repository.GetByKeyAsync("ab-test", Arg.Any<CancellationToken>()).Returns(flag);

        await _sut.GetVariantAsync("ab-test", Guid.NewGuid());

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    #endregion

    #region Helpers

    private static void AddOverride(FeatureFlag flag, FeatureFlagOverride @override)
    {
        // Use reflection to add to the private _overrides list
        System.Reflection.FieldInfo? field = typeof(FeatureFlag)
            .GetField("_overrides", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        List<FeatureFlagOverride>? overrides = field?.GetValue(flag) as List<FeatureFlagOverride>;
        overrides?.Add(@override);
    }

    #endregion
}
