using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Events;
using Foundry.Configuration.Domain.ValueObjects;
using Foundry.Configuration.Infrastructure.Services;
using Wolverine;

namespace Foundry.Configuration.Tests.Infrastructure;

public class FeatureFlagServiceIsEnabledTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceIsEnabledTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _service = new(_repository, _messageBus, TimeProvider.System);
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagNotFound_ReturnsFalse()
    {
        _repository.GetByKeyAsync("missing", Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        bool result = await _service.IsEnabledAsync("missing", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenBooleanFlagEnabled_ReturnsTrue()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenBooleanFlagDisabled_ReturnsFalse()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", false, TimeProvider.System);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_PublishesEvaluationEvent()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        await _service.IsEnabledAsync("feature", tenantId, userId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<FeatureFlagEvaluatedEvent>(e =>
            e.FlagKey == "feature" &&
            e.TenantId == tenantId &&
            e.UserId == userId &&
            e.Result == "True"));
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageAtZero_ReturnsFalse()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 0, TimeProvider.System);
        _repository.GetByKeyAsync("rollout", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("rollout", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageAt100_ReturnsTrue()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 100, TimeProvider.System);
        _repository.GetByKeyAsync("rollout", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("rollout", Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_PercentageWithoutUserId_ReturnsTrueWhenPercentageAboveZero()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 50, TimeProvider.System);
        _repository.GetByKeyAsync("rollout", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("rollout", Guid.NewGuid(), userId: null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WithUserTenantOverride_UsesOverride()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", false, TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenantUser(flag.Id, tenantId, userId, true, TimeProvider.System);
        AddOverrideToFlag(flag, overrideEntity);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", tenantId, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WithTenantOverride_UsesOverride()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, false, TimeProvider.System);
        AddOverrideToFlag(flag, overrideEntity);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", tenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WithExpiredOverride_IgnoresOverride()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, false, TimeProvider.System, expiresAt: DateTime.UtcNow.AddMinutes(-5));
        AddOverrideToFlag(flag, overrideEntity);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", tenantId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_UserTenantOverrideTakesPriority()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", false, TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        FeatureFlagOverride tenantOverride = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, false, TimeProvider.System);
        FeatureFlagOverride userTenantOverride = FeatureFlagOverride.CreateForTenantUser(flag.Id, tenantId, userId, true, TimeProvider.System);

        AddOverrideToFlag(flag, tenantOverride);
        AddOverrideToFlag(flag, userTenantOverride);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", tenantId, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_UserOnlyOverrideTakesPriorityOverTenant()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", false, TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        FeatureFlagOverride tenantOverride = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, false, TimeProvider.System);
        FeatureFlagOverride userOverride = FeatureFlagOverride.CreateForUser(flag.Id, userId, true, TimeProvider.System);

        AddOverrideToFlag(flag, tenantOverride);
        AddOverrideToFlag(flag, userOverride);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        bool result = await _service.IsEnabledAsync("feature", tenantId, userId);

        result.Should().BeTrue();
    }

    private static void AddOverrideToFlag(FeatureFlag flag, FeatureFlagOverride overrideEntity)
    {
        // Use reflection to add override to the backing list since there's no public method
        System.Reflection.FieldInfo? field = typeof(FeatureFlag)
            .GetField("_overrides", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        List<FeatureFlagOverride>? overrides = field?.GetValue(flag) as List<FeatureFlagOverride>;
        overrides?.Add(overrideEntity);
    }
}

public class FeatureFlagServiceGetVariantTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceGetVariantTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _service = new(_repository, _messageBus, TimeProvider.System);
    }

    [Fact]
    public async Task GetVariantAsync_WhenFlagNotFound_ReturnsNull()
    {
        _repository.GetByKeyAsync("missing", Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        string? result = await _service.GetVariantAsync("missing", Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVariantAsync_ForVariantFlag_ReturnsVariant()
    {
        VariantWeight[] variants =
        [
            new("control", 100),
            new("treatment", 0)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", variants, "control", TimeProvider.System);
        _repository.GetByKeyAsync("ab_test", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _service.GetVariantAsync("ab_test", Guid.NewGuid(), Guid.NewGuid());

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVariantAsync_ForBooleanFlag_ReturnsDefaultVariant()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _service.GetVariantAsync("feature", Guid.NewGuid());

        result.Should().BeNull(); // Boolean flags have no DefaultVariant
    }

    [Fact]
    public async Task GetVariantAsync_WithOverride_UsesOverrideVariant()
    {
        VariantWeight[] variants =
        [
            new("control", 50),
            new("treatment", 50)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", variants, "control", TimeProvider.System);
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flag.Id, tenantId, null, TimeProvider.System, variant: "treatment");

        System.Reflection.FieldInfo? field = typeof(FeatureFlag)
            .GetField("_overrides", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        (field?.GetValue(flag) as List<FeatureFlagOverride>)?.Add(overrideEntity);

        _repository.GetByKeyAsync("ab_test", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _service.GetVariantAsync("ab_test", tenantId);

        result.Should().Be("treatment");
    }

    [Fact]
    public async Task GetVariantAsync_PublishesEvaluationEvent()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);
        Guid tenantId = Guid.NewGuid();

        await _service.GetVariantAsync("feature", tenantId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<FeatureFlagEvaluatedEvent>(e =>
            e.FlagKey == "feature" && e.TenantId == tenantId));
    }

    [Fact]
    public async Task GetVariantAsync_ForVariantFlagWithAllZeroWeights_ReturnsFirstVariant()
    {
        VariantWeight[] variants =
        [
            new("control", 0),
            new("treatment", 0)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", variants, "control", TimeProvider.System);
        _repository.GetByKeyAsync("ab_test", Arg.Any<CancellationToken>()).Returns(flag);

        string? result = await _service.GetVariantAsync("ab_test", Guid.NewGuid(), Guid.NewGuid());

        result.Should().Be("control");
    }
}

public class FeatureFlagServiceGetAllFlagsTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceGetAllFlagsTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        _service = new(_repository, messageBus, TimeProvider.System);
    }

    [Fact]
    public async Task GetAllFlagsAsync_WithNoFlags_ReturnsEmptyDictionary()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag>());

        Dictionary<string, object> result = await _service.GetAllFlagsAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllFlagsAsync_WithBooleanFlag_ReturnsBooleanValue()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { flag });
        _repository.GetByKeyAsync("feature", Arg.Any<CancellationToken>()).Returns(flag);

        Dictionary<string, object> result = await _service.GetAllFlagsAsync(Guid.NewGuid());

        result.Should().ContainKey("feature");
        result["feature"].Should().Be(true);
    }

    [Fact]
    public async Task GetAllFlagsAsync_WithVariantFlag_ReturnsVariantValue()
    {
        VariantWeight[] variants =
        [
            new("control", 100)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", variants, "control", TimeProvider.System);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { flag });
        _repository.GetByKeyAsync("ab_test", Arg.Any<CancellationToken>()).Returns(flag);

        Dictionary<string, object> result = await _service.GetAllFlagsAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().ContainKey("ab_test");
    }

    [Fact]
    public async Task GetAllFlagsAsync_WithMultipleFlags_ReturnsAll()
    {
        FeatureFlag boolFlag = FeatureFlag.CreateBoolean("bool_flag", "Bool", true, TimeProvider.System);
        FeatureFlag percentFlag = FeatureFlag.CreatePercentage("pct_flag", "Pct", 50, TimeProvider.System);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { boolFlag, percentFlag });
        _repository.GetByKeyAsync("bool_flag", Arg.Any<CancellationToken>()).Returns(boolFlag);
        _repository.GetByKeyAsync("pct_flag", Arg.Any<CancellationToken>()).Returns(percentFlag);

        Dictionary<string, object> result = await _service.GetAllFlagsAsync(Guid.NewGuid());

        result.Should().HaveCount(2);
        result.Should().ContainKey("bool_flag");
        result.Should().ContainKey("pct_flag");
    }
}
