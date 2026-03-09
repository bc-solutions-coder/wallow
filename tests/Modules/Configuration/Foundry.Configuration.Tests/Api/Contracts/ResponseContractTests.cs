using Foundry.Configuration.Api.Contracts.Enums;
using Foundry.Configuration.Api.Contracts.Responses;
using Foundry.Configuration.Application.FeatureFlags.DTOs;

namespace Foundry.Configuration.Tests.Api.Contracts;

public class ResponseContractTests
{
    #region FeatureFlagResponse

    [Fact]
    public void FeatureFlagResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow.AddDays(-1);
        DateTime updatedAt = DateTime.UtcNow;
        List<VariantWeightDto> variants = [new("control", 50),
            new("treatment", 50)];

        FeatureFlagResponse response = new(id, "dark-mode", "Dark Mode", "Toggle dark theme",
            ApiFlagType.Variant, false, 50, variants, "control", createdAt, updatedAt);

        response.Id.Should().Be(id);
        response.Key.Should().Be("dark-mode");
        response.Name.Should().Be("Dark Mode");
        response.Description.Should().Be("Toggle dark theme");
        response.FlagType.Should().Be(ApiFlagType.Variant);
        response.DefaultEnabled.Should().BeFalse();
        response.RolloutPercentage.Should().Be(50);
        response.Variants.Should().HaveCount(2);
        response.DefaultVariant.Should().Be("control");
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void FeatureFlagResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        FeatureFlagResponse response = new(Guid.NewGuid(), "toggle", "Toggle", null,
            ApiFlagType.Boolean, true, null, null, null, DateTime.UtcNow, null);

        response.Description.Should().BeNull();
        response.RolloutPercentage.Should().BeNull();
        response.Variants.Should().BeNull();
        response.DefaultVariant.Should().BeNull();
        response.UpdatedAt.Should().BeNull();
    }

    #endregion

    #region FeatureFlagOverrideResponse

    [Fact]
    public void FeatureFlagOverrideResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        Guid flagId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);
        DateTime createdAt = DateTime.UtcNow;

        FeatureFlagOverrideResponse response = new(id, flagId, tenantId, userId, true, "variant-a", expiresAt, createdAt);

        response.Id.Should().Be(id);
        response.FlagId.Should().Be(flagId);
        response.TenantId.Should().Be(tenantId);
        response.UserId.Should().Be(userId);
        response.IsEnabled.Should().BeTrue();
        response.Variant.Should().Be("variant-a");
        response.ExpiresAt.Should().Be(expiresAt);
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void FeatureFlagOverrideResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        FeatureFlagOverrideResponse response = new(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, DateTime.UtcNow);

        response.TenantId.Should().BeNull();
        response.UserId.Should().BeNull();
        response.IsEnabled.Should().BeNull();
        response.Variant.Should().BeNull();
        response.ExpiresAt.Should().BeNull();
    }

    #endregion
}
