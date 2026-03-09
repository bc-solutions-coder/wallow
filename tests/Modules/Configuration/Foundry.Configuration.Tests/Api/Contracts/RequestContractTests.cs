using Foundry.Configuration.Api.Contracts.Enums;
using Foundry.Configuration.Api.Contracts.Requests;
using Foundry.Configuration.Api.Controllers;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Shared.Kernel.CustomFields;

namespace Foundry.Configuration.Tests.Api.Contracts;

public class RequestContractTests
{
    #region CreateFeatureFlagRequest

    [Fact]
    public void CreateFeatureFlagRequest_WithAllFields_CreatesInstance()
    {
        List<VariantWeightDto> variants = [new("a", 50), new("b", 50)];
        CreateFeatureFlagRequest request = new("dark-mode", "Dark Mode", "A dark theme", ApiFlagType.Variant, true, 50, variants, "a");

        request.Key.Should().Be("dark-mode");
        request.Name.Should().Be("Dark Mode");
        request.Description.Should().Be("A dark theme");
        request.FlagType.Should().Be(ApiFlagType.Variant);
        request.DefaultEnabled.Should().BeTrue();
        request.RolloutPercentage.Should().Be(50);
        request.Variants.Should().HaveCount(2);
        request.DefaultVariant.Should().Be("a");
    }

    [Fact]
    public void CreateFeatureFlagRequest_WithNullOptionalFields_CreatesInstance()
    {
        CreateFeatureFlagRequest request = new("toggle", "Toggle", null, ApiFlagType.Boolean, false, null, null, null);

        request.Description.Should().BeNull();
        request.RolloutPercentage.Should().BeNull();
        request.Variants.Should().BeNull();
        request.DefaultVariant.Should().BeNull();
    }

    #endregion

    #region UpdateFeatureFlagRequest

    [Fact]
    public void UpdateFeatureFlagRequest_WithAllFields_CreatesInstance()
    {
        UpdateFeatureFlagRequest request = new("Updated Name", "Updated Desc", true, 75);

        request.Name.Should().Be("Updated Name");
        request.Description.Should().Be("Updated Desc");
        request.DefaultEnabled.Should().BeTrue();
        request.RolloutPercentage.Should().Be(75);
    }

    [Fact]
    public void UpdateFeatureFlagRequest_WithNullOptionalFields_CreatesInstance()
    {
        UpdateFeatureFlagRequest request = new("Name", null, false, null);

        request.Description.Should().BeNull();
        request.RolloutPercentage.Should().BeNull();
    }

    #endregion

    #region CreateOverrideRequest

    [Fact]
    public void CreateOverrideRequest_WithAllFields_CreatesInstance()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        CreateOverrideRequest request = new(tenantId, userId, true, "variant-a", expiresAt);

        request.TenantId.Should().Be(tenantId);
        request.UserId.Should().Be(userId);
        request.IsEnabled.Should().BeTrue();
        request.Variant.Should().Be("variant-a");
        request.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void CreateOverrideRequest_WithNullOptionalFields_CreatesInstance()
    {
        CreateOverrideRequest request = new(null, null, null, null, null);

        request.TenantId.Should().BeNull();
        request.UserId.Should().BeNull();
        request.IsEnabled.Should().BeNull();
        request.Variant.Should().BeNull();
        request.ExpiresAt.Should().BeNull();
    }

    #endregion

    #region CreateCustomFieldRequest

    [Fact]
    public void CreateCustomFieldRequest_WithAllFields_CreatesInstance()
    {
        FieldValidationRules rules = new() { MinLength = 1, MaxLength = 255 };
        List<CustomFieldOption> options = [

            new() { Value = "low", Label = "Low", Order = 0 },
            new() { Value = "high", Label = "High", Order = 1 }
        ];
        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "priority",
            DisplayName = "Priority",
            FieldType = CustomFieldType.Dropdown,
            Description = "Priority level",
            IsRequired = true,
            ValidationRules = rules,
            Options = options
        };

        request.EntityType.Should().Be("Invoice");
        request.FieldKey.Should().Be("priority");
        request.DisplayName.Should().Be("Priority");
        request.FieldType.Should().Be(CustomFieldType.Dropdown);
        request.Description.Should().Be("Priority level");
        request.IsRequired.Should().BeTrue();
        request.ValidationRules.Should().NotBeNull();
        request.ValidationRules!.MinLength.Should().Be(1);
        request.Options.Should().HaveCount(2);
    }

    [Fact]
    public void CreateCustomFieldRequest_WithNullOptionalFields_CreatesInstance()
    {
        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "ref",
            DisplayName = "Reference",
            FieldType = CustomFieldType.Text
        };

        request.Description.Should().BeNull();
        request.IsRequired.Should().BeFalse();
        request.ValidationRules.Should().BeNull();
        request.Options.Should().BeNull();
    }

    #endregion

    #region UpdateCustomFieldRequest

    [Fact]
    public void UpdateCustomFieldRequest_WithAllFields_CreatesInstance()
    {
        FieldValidationRules rules = new() { Min = 0, Max = 100 };
        List<CustomFieldOption> options = [

            new() { Value = "a", Label = "A" }
        ];
        UpdateCustomFieldRequest request = new()
        {
            DisplayName = "Updated",
            Description = "Updated desc",
            IsRequired = true,
            DisplayOrder = 5,
            ValidationRules = rules,
            Options = options
        };

        request.DisplayName.Should().Be("Updated");
        request.Description.Should().Be("Updated desc");
        request.IsRequired.Should().BeTrue();
        request.DisplayOrder.Should().Be(5);
        request.ValidationRules.Should().NotBeNull();
        request.Options.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateCustomFieldRequest_WithNullFields_CreatesInstance()
    {
        UpdateCustomFieldRequest request = new();

        request.DisplayName.Should().BeNull();
        request.Description.Should().BeNull();
        request.IsRequired.Should().BeNull();
        request.DisplayOrder.Should().BeNull();
        request.ValidationRules.Should().BeNull();
        request.Options.Should().BeNull();
    }

    #endregion

    #region ReorderFieldsRequest

    [Fact]
    public void ReorderFieldsRequest_WithFieldIds_CreatesInstance()
    {
        List<Guid> fieldIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        ReorderFieldsRequest request = new() { FieldIds = fieldIds };

        request.FieldIds.Should().HaveCount(3);
        request.FieldIds.Should().BeEquivalentTo(fieldIds);
    }

    #endregion
}
