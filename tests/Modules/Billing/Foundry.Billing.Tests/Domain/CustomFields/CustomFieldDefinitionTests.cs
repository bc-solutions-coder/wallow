using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Events;
using Foundry.Billing.Domain.CustomFields.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Tests.Domain.CustomFields;

public class CustomFieldDefinitionCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsDefinitionWithCorrectFields()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number",
            CustomFieldType.Text, createdBy, TimeProvider.System);

        definition.TenantId.Should().Be(tenantId);
        definition.EntityType.Should().Be("Invoice");
        definition.FieldKey.Should().Be("po_number");
        definition.DisplayName.Should().Be("PO Number");
        definition.FieldType.Should().Be(CustomFieldType.Text);
        definition.IsActive.Should().BeTrue();
        definition.IsRequired.Should().BeFalse();
        definition.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void Create_RaisesCustomFieldDefinitionCreatedEvent()
    {
        TenantId tenantId = TenantId.New();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "notes", "Notes",
            CustomFieldType.TextArea, Guid.NewGuid(), TimeProvider.System);

        definition.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomFieldDefinitionCreatedEvent>()
            .Which.Should().Match<CustomFieldDefinitionCreatedEvent>(e =>
                e.DefinitionId == definition.Id.Value &&
                e.TenantId == tenantId.Value &&
                e.EntityType == "Invoice" &&
                e.FieldKey == "notes" &&
                e.DisplayName == "Notes" &&
                e.FieldType == CustomFieldType.TextArea);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyEntityType_ThrowsCustomFieldException(string entityType)
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), entityType, "field_key", "Display",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Entity type is required");
    }

    [Fact]
    public void Create_WithUnsupportedEntityType_ThrowsCustomFieldException()
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "UnknownEntity", "field_key", "Display",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*does not support custom fields*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFieldKey_ThrowsCustomFieldException(string fieldKey)
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, "Display",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Field key is required");
    }

    [Fact]
    public void Create_WithFieldKeyExceeding50Chars_ThrowsCustomFieldException()
    {
        string longKey = new string('a', 51);

        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", longKey, "Display",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Field key must be 50 characters or less");
    }

    [Theory]
    [InlineData("InvalidKey")]
    [InlineData("1invalid")]
    [InlineData("has space")]
    [InlineData("has-dash")]
    public void Create_WithInvalidFieldKeyPattern_ThrowsCustomFieldException(string fieldKey)
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, "Display",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*lowercase alphanumeric*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDisplayName_ThrowsCustomFieldException(string displayName)
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "field_key", displayName,
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Display name is required");
    }

    [Fact]
    public void Create_WithDisplayNameExceeding100Chars_ThrowsCustomFieldException()
    {
        string longName = new string('A', 101);

        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "field_key", longName,
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Display name must be 100 characters or less");
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("Payment")]
    [InlineData("Subscription")]
    public void Create_WithSupportedEntityTypes_Succeeds(string entityType)
    {
        Action act = () => CustomFieldDefinition.Create(
            TenantId.New(), entityType, "field_key", "Label",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }
}

public class CustomFieldDefinitionUpdateTests
{
    private static CustomFieldDefinition CreateDefinition(CustomFieldType fieldType = CustomFieldType.Text)
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "test_field", "Test Field",
            fieldType, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }

    [Fact]
    public void UpdateDisplayName_WithValidName_UpdatesName()
    {
        CustomFieldDefinition definition = CreateDefinition();
        Guid updatedBy = Guid.NewGuid();

        definition.UpdateDisplayName("New Name", updatedBy, TimeProvider.System);

        definition.DisplayName.Should().Be("New Name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithEmptyName_ThrowsCustomFieldException(string name)
    {
        CustomFieldDefinition definition = CreateDefinition();

        Action act = () => definition.UpdateDisplayName(name, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>();
    }

    [Fact]
    public void UpdateDescription_WithValue_SetsDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();

        definition.UpdateDescription("Some description", Guid.NewGuid(), TimeProvider.System);

        definition.Description.Should().Be("Some description");
    }

    [Fact]
    public void UpdateDescription_WithNull_ClearsDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();
        definition.UpdateDescription("initial", Guid.NewGuid(), TimeProvider.System);

        definition.UpdateDescription(null, Guid.NewGuid(), TimeProvider.System);

        definition.Description.Should().BeNull();
    }

    [Fact]
    public void SetRequired_ToTrue_SetsIsRequired()
    {
        CustomFieldDefinition definition = CreateDefinition();

        definition.SetRequired(true, Guid.NewGuid(), TimeProvider.System);

        definition.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void SetDisplayOrder_WithValidOrder_SetsOrder()
    {
        CustomFieldDefinition definition = CreateDefinition();

        definition.SetDisplayOrder(5, Guid.NewGuid(), TimeProvider.System);

        definition.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void SetDisplayOrder_WithNegativeValue_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition();

        Action act = () => definition.SetDisplayOrder(-1, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Display order cannot be negative");
    }

    [Fact]
    public void SetDisplayOrder_WithZero_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition();

        Action act = () => definition.SetDisplayOrder(0, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }
}

public class CustomFieldDefinitionValidationRulesTests
{
    [Fact]
    public void SetValidationRules_WithTextFieldAndLengthRules_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { MinLength = 1, MaxLength = 50 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetValidationRules_WithNonTextFieldAndLengthRules_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Number);
        FieldValidationRules rules = new() { MinLength = 1 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*MinLength/MaxLength*");
    }

    [Fact]
    public void SetValidationRules_WithNumberFieldAndNumericRules_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Number);
        FieldValidationRules rules = new() { Min = 0, Max = 100 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetValidationRules_WithTextFieldAndNumericRules_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { Min = 0 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Min/Max rules*");
    }

    [Fact]
    public void SetValidationRules_WithDateFieldAndDateRules_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Date);
        FieldValidationRules rules = new() { MinDate = DateTime.UtcNow.AddDays(-30) };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetValidationRules_WithNonDateFieldAndDateRules_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { MinDate = DateTime.UtcNow };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*MinDate/MaxDate*");
    }

    [Fact]
    public void SetValidationRules_WithTextFieldAndPatternRule_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { Pattern = @"^\d{4}$" };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetValidationRules_WithNonTextFieldAndPatternRule_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Number);
        FieldValidationRules rules = new() { Pattern = @"^\d+$" };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Pattern rules*");
    }

    [Fact]
    public void SetValidationRules_WithInvalidRegexPattern_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { Pattern = "[invalid" };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Invalid regex pattern");
    }

    [Fact]
    public void SetValidationRules_WithNull_ClearsRules()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        definition.SetValidationRules(new() { MinLength = 5 }, Guid.NewGuid(), TimeProvider.System);

        definition.SetValidationRules(null, Guid.NewGuid(), TimeProvider.System);

        definition.GetValidationRules().Should().BeNull();
    }

    [Fact]
    public void GetValidationRules_WhenRulesSet_ReturnsDeserializedRules()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new() { MinLength = 3, MaxLength = 50 };
        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        FieldValidationRules? retrieved = definition.GetValidationRules();

        retrieved.Should().NotBeNull();
        retrieved!.MinLength.Should().Be(3);
        retrieved.MaxLength.Should().Be(50);
    }

    private static CustomFieldDefinition CreateDefinition(CustomFieldType fieldType = CustomFieldType.Text)
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "test_field", "Test Field",
            fieldType, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}

public class CustomFieldDefinitionOptionsTests
{
    [Fact]
    public void SetOptions_WithDropdownField_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "active", Label = "Active" },
            new() { Value = "inactive", Label = "Inactive" }
        ];

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetOptions_WithMultiSelectField_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.MultiSelect);
        List<CustomFieldOption> options =
        [
            new() { Value = "opt1", Label = "Option 1" },
            new() { Value = "opt2", Label = "Option 2" }
        ];

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetOptions_WithNonDropdownField_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        List<CustomFieldOption> options = [new() { Value = "opt1", Label = "Option 1" }];

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*only allowed for Dropdown and MultiSelect*");
    }

    [Fact]
    public void SetOptions_WithDuplicateValues_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "dup", Label = "First" },
            new() { Value = "dup", Label = "Second" }
        ];

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Duplicate option values*");
    }

    [Fact]
    public void SetOptions_WithNull_ClearsOptions()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Dropdown);
        definition.SetOptions([new() { Value = "opt1", Label = "Opt" }], Guid.NewGuid(), TimeProvider.System);

        definition.SetOptions(null, Guid.NewGuid(), TimeProvider.System);

        definition.GetOptions().Should().BeEmpty();
    }

    [Fact]
    public void GetOptions_WhenOptionsSet_ReturnsDeserializedOptions()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "yes", Label = "Yes" },
            new() { Value = "no", Label = "No" }
        ];
        definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        IReadOnlyList<CustomFieldOption> retrieved = definition.GetOptions();

        retrieved.Should().HaveCount(2);
        retrieved[0].Value.Should().Be("yes");
        retrieved[1].Value.Should().Be("no");
    }

    [Fact]
    public void GetOptions_WhenNoOptionsSet_ReturnsEmptyList()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Dropdown);

        IReadOnlyList<CustomFieldOption> retrieved = definition.GetOptions();

        retrieved.Should().BeEmpty();
    }

    private static CustomFieldDefinition CreateDefinition(CustomFieldType fieldType = CustomFieldType.Dropdown)
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "status_field", "Status",
            fieldType, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}

public class CustomFieldDefinitionActivationTests
{
    [Fact]
    public void Deactivate_ActiveField_SetsIsActiveFalse()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();

        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        definition.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ActiveField_RaisesDeactivatedEvent()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();

        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        definition.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomFieldDefinitionDeactivatedEvent>()
            .Which.Should().Match<CustomFieldDefinitionDeactivatedEvent>(e =>
                e.DefinitionId == definition.Id.Value &&
                e.EntityType == "Invoice" &&
                e.FieldKey == "test_field");
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();
        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();

        Action act = () => definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Field is already deactivated");
    }

    [Fact]
    public void Activate_InactiveField_SetsIsActiveTrue()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();
        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();

        definition.Activate(Guid.NewGuid(), TimeProvider.System);

        definition.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_AlreadyActive_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();

        Action act = () => definition.Activate(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("Field is already active");
    }

    private static CustomFieldDefinition CreateActiveDefinition()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "test_field", "Test Field",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}
