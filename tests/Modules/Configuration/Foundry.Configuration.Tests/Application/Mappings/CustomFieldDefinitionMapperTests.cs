using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Application.Mappings;

public class CustomFieldDefinitionMapperTests
{
    private static CustomFieldDefinition CreateDefinition(
        string entityType = "Invoice",
        string fieldKey = "test_field",
        CustomFieldType fieldType = CustomFieldType.Text)
    {
        TenantId tenantId = TenantId.New();
        return CustomFieldDefinition.Create(tenantId, entityType, fieldKey, "Test Field", fieldType, Guid.Empty, TimeProvider.System);
    }

    [Fact]
    public void ToDto_MapsAllProperties()
    {
        CustomFieldDefinition definition = CreateDefinition();

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Id.Should().Be(definition.Id.Value);
        dto.EntityType.Should().Be("Invoice");
        dto.FieldKey.Should().Be("test_field");
        dto.DisplayName.Should().Be("Test Field");
        dto.FieldType.Should().Be(CustomFieldType.Text);
        dto.DisplayOrder.Should().Be(0);
        dto.IsRequired.Should().BeFalse();
        dto.IsActive.Should().BeTrue();
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ToDto_WithDescription_MapsDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();
        definition.UpdateDescription("A description", Guid.Empty, TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Description.Should().Be("A description");
    }

    [Fact]
    public void ToDto_WithValidationRules_MapsRules()
    {
        CustomFieldDefinition definition = CreateDefinition();
        FieldValidationRules rules = new() { MaxLength = 100, MinLength = 5 };
        definition.SetValidationRules(rules, Guid.Empty, TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.ValidationRules.Should().NotBeNull();
        dto.ValidationRules!.MaxLength.Should().Be(100);
        dto.ValidationRules!.MinLength.Should().Be(5);
    }

    [Fact]
    public void ToDto_WithOptions_MapsOptions()
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType: CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new CustomFieldOption { Value = "a", Label = "Option A" },
            new CustomFieldOption { Value = "b", Label = "Option B" }
        ];
        definition.SetOptions(options, Guid.Empty, TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Options.Should().HaveCount(2);
        dto.Options![0].Value.Should().Be("a");
        dto.Options![1].Value.Should().Be("b");
    }

    [Fact]
    public void ToDto_WhenDeactivated_MapsIsActiveFalse()
    {
        CustomFieldDefinition definition = CreateDefinition();
        definition.Deactivate(Guid.Empty, TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ToDtoList_MapsMultipleDefinitions()
    {
        List<CustomFieldDefinition> definitions =
        [
            CreateDefinition(fieldKey: "field_a"),
            CreateDefinition(fieldKey: "field_b"),
            CreateDefinition(fieldKey: "field_c")
        ];

        IReadOnlyList<CustomFieldDefinitionDto> dtos = definitions.ToDtoList();

        dtos.Should().HaveCount(3);
        dtos[0].FieldKey.Should().Be("field_a");
        dtos[1].FieldKey.Should().Be("field_b");
        dtos[2].FieldKey.Should().Be("field_c");
    }

    [Fact]
    public void ToDtoList_WithEmptyList_ReturnsEmptyList()
    {
        List<CustomFieldDefinition> definitions = [];

        IReadOnlyList<CustomFieldDefinitionDto> dtos = definitions.ToDtoList();

        dtos.Should().BeEmpty();
    }
}
