using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Tests.Application.CustomFields;

public class CustomFieldDefinitionMapperTests
{
    [Fact]
    public void ToDto_MapsAllBasicFields()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "po_number", "PO Number",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Id.Should().Be(definition.Id.Value);
        dto.EntityType.Should().Be("Invoice");
        dto.FieldKey.Should().Be("po_number");
        dto.DisplayName.Should().Be("PO Number");
        dto.FieldType.Should().Be(CustomFieldType.Text);
        dto.IsActive.Should().BeTrue();
        dto.IsRequired.Should().BeFalse();
        dto.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void ToDto_WithDescription_MapsDescription()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "notes_field", "Notes",
            CustomFieldType.TextArea, Guid.NewGuid(), TimeProvider.System);
        definition.UpdateDescription("A helpful description", Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Description.Should().Be("A helpful description");
    }

    [Fact]
    public void ToDto_WithValidationRules_MapsRules()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "text_field", "Text",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        definition.SetValidationRules(new() { MinLength = 5, MaxLength = 50 }, Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.ValidationRules.Should().NotBeNull();
        dto.ValidationRules!.MinLength.Should().Be(5);
        dto.ValidationRules.MaxLength.Should().Be(50);
    }

    [Fact]
    public void ToDto_WithOptions_MapsOptions()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "status_field", "Status",
            CustomFieldType.Dropdown, Guid.NewGuid(), TimeProvider.System);
        definition.SetOptions(
            [new() { Value = "yes", Label = "Yes" }, new() { Value = "no", Label = "No" }],
            Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.Options.Should().HaveCount(2);
    }

    [Fact]
    public void ToDto_WithNullValidationRules_ReturnsDtoWithNullRules()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "simple_field", "Simple",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDto dto = definition.ToDto();

        dto.ValidationRules.Should().BeNull();
    }

    [Fact]
    public void ToDtoList_WithMultipleEntities_ReturnsAllMapped()
    {
        List<CustomFieldDefinition> definitions =
        [
            CreateDefinition("field_one"),
            CreateDefinition("field_two"),
            CreateDefinition("field_three")
        ];

        IReadOnlyList<CustomFieldDefinitionDto> dtos = definitions.ToDtoList();

        dtos.Should().HaveCount(3);
        dtos.Select(d => d.FieldKey).Should().Contain(["field_one", "field_two", "field_three"]);
    }

    [Fact]
    public void ToDtoList_WithEmptyList_ReturnsEmptyList()
    {
        List<CustomFieldDefinition> definitions = [];

        IReadOnlyList<CustomFieldDefinitionDto> dtos = definitions.ToDtoList();

        dtos.Should().BeEmpty();
    }

    private static CustomFieldDefinition CreateDefinition(string fieldKey)
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, fieldKey.Replace("_", " "),
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}
