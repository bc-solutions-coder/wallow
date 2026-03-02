using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;

namespace Foundry.Configuration.Tests.Domain;

public class FeatureFlagOverrideCreateForTenantTests
{
    [Fact]
    public void CreateForTenant_WithValidData_ReturnsTenantOverride()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, tenantId, true, TimeProvider.System);

        overrideEntity.FlagId.Should().Be(flagId);
        overrideEntity.TenantId.Should().Be(tenantId);
        overrideEntity.UserId.Should().BeNull();
        overrideEntity.IsEnabled.Should().BeTrue();
        overrideEntity.Variant.Should().BeNull();
        overrideEntity.ExpiresAt.Should().BeNull();
        overrideEntity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateForTenant_WithDisabled_SetsIsEnabledFalse()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, tenantId, false, TimeProvider.System);

        overrideEntity.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CreateForTenant_WithVariant_SetsVariant()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, tenantId, null, TimeProvider.System, variant: "treatment");

        overrideEntity.IsEnabled.Should().BeNull();
        overrideEntity.Variant.Should().Be("treatment");
    }

    [Fact]
    public void CreateForTenant_WithExpiration_SetsExpiresAt()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, tenantId, true, TimeProvider.System, expiresAt: expiresAt);

        overrideEntity.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }
}

public class FeatureFlagOverrideCreateForUserTests
{
    [Fact]
    public void CreateForUser_WithValidData_ReturnsUserOverride()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid userId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForUser(flagId, userId, true, TimeProvider.System);

        overrideEntity.FlagId.Should().Be(flagId);
        overrideEntity.TenantId.Should().BeNull();
        overrideEntity.UserId.Should().Be(userId);
        overrideEntity.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreateForUser_WithVariant_SetsVariant()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid userId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForUser(flagId, userId, null, TimeProvider.System, variant: "control");

        overrideEntity.Variant.Should().Be("control");
        overrideEntity.UserId.Should().Be(userId);
    }
}

public class FeatureFlagOverrideCreateForTenantUserTests
{
    [Fact]
    public void CreateForTenantUser_WithValidData_ReturnsTenantUserOverride()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenantUser(flagId, tenantId, userId, true, TimeProvider.System);

        overrideEntity.FlagId.Should().Be(flagId);
        overrideEntity.TenantId.Should().Be(tenantId);
        overrideEntity.UserId.Should().Be(userId);
        overrideEntity.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreateForTenantUser_WithVariantAndExpiration_SetsBoth()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddHours(24);

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenantUser(flagId, tenantId, userId, null, TimeProvider.System, variant: "treatment_b", expiresAt: expiresAt);

        overrideEntity.Variant.Should().Be("treatment_b");
        overrideEntity.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        overrideEntity.IsEnabled.Should().BeNull();
    }
}

public class FeatureFlagOverrideExpirationTests
{
    [Fact]
    public void IsExpired_WithPastExpiration_ReturnsTrue()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        DateTime pastExpiration = DateTime.UtcNow.AddMinutes(-1);

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, Guid.NewGuid(), true, TimeProvider.System, expiresAt: pastExpiration);

        overrideEntity.IsExpired(TimeProvider.System).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        DateTime futureExpiration = DateTime.UtcNow.AddDays(7);

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, Guid.NewGuid(), true, TimeProvider.System, expiresAt: futureExpiration);

        overrideEntity.IsExpired(TimeProvider.System).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithNoExpiration_ReturnsFalse()
    {
        FeatureFlagId flagId = FeatureFlagId.New();

        FeatureFlagOverride overrideEntity = FeatureFlagOverride.CreateForTenant(flagId, Guid.NewGuid(), true, TimeProvider.System);

        overrideEntity.IsExpired(TimeProvider.System).Should().BeFalse();
    }
}

public class FeatureFlagOverrideIdGenerationTests
{
    [Fact]
    public void Create_MultipleTimes_GeneratesUniqueIds()
    {
        FeatureFlagId flagId = FeatureFlagId.New();

        FeatureFlagOverride override1 = FeatureFlagOverride.CreateForTenant(flagId, Guid.NewGuid(), true, TimeProvider.System);
        FeatureFlagOverride override2 = FeatureFlagOverride.CreateForUser(flagId, Guid.NewGuid(), false, TimeProvider.System);

        override1.Id.Should().NotBe(override2.Id);
    }
}
