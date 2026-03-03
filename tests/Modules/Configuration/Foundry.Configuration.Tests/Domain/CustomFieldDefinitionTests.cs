using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Events;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Domain;

public class CustomFieldDefinitionCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsActiveDefinition()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Text, createdBy, TimeProvider.System);

        definition.Id.Value.Should().NotBeEmpty();
        definition.TenantId.Should().Be(tenantId);
        definition.EntityType.Should().Be("Invoice");
        definition.FieldKey.Should().Be("po_number");
        definition.DisplayName.Should().Be("PO Number");
        definition.FieldType.Should().Be(CustomFieldType.Text);
        definition.DisplayOrder.Should().Be(0);
        definition.IsRequired.Should().BeFalse();
        definition.IsActive.Should().BeTrue();
        definition.ValidationRulesJson.Should().BeNull();
        definition.OptionsJson.Should().BeNull();
        definition.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidData_SetsCreatedBy()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Text, createdBy, TimeProvider.System);

        definition.CreatedBy.Should().Be(createdBy);
        definition.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithValidData_RaisesCreatedEvent()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Text, createdBy, TimeProvider.System);

        definition.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomFieldDefinitionCreatedEvent>()
            .Which.EntityType.Should().Be("Invoice");
    }

    [Fact]
    public void Create_WithValidData_CreatedEventHasCorrectProperties()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Number, createdBy, TimeProvider.System);

        CustomFieldDefinitionCreatedEvent evt = definition.DomainEvents.OfType<CustomFieldDefinitionCreatedEvent>().Single();
        evt.DefinitionId.Should().Be(definition.Id.Value);
        evt.TenantId.Should().Be(tenantId.Value);
        evt.FieldKey.Should().Be("po_number");
        evt.DisplayName.Should().Be("PO Number");
        evt.FieldType.Should().Be(CustomFieldType.Number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyEntityType_ThrowsCustomFieldException(string entityType)
    {
        TenantId tenantId = TenantId.New();

        Action act = () => CustomFieldDefinition.Create(
            tenantId, entityType, "po_number", "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Entity type is required*");
    }

    [Fact]
    public void Create_WithUnsupportedEntityType_ThrowsCustomFieldException()
    {
        TenantId tenantId = TenantId.New();

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "UnsupportedType", "po_number", "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*does not support custom fields*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFieldKey_ThrowsCustomFieldException(string fieldKey)
    {
        TenantId tenantId = TenantId.New();

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "Invoice", fieldKey, "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Field key is required*");
    }

    [Fact]
    public void Create_WithFieldKeyTooLong_ThrowsCustomFieldException()
    {
        TenantId tenantId = TenantId.New();
        string longKey = new string('a', 51);

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "Invoice", longKey, "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*50 characters*");
    }

    [Theory]
    [InlineData("PO_Number")]
    [InlineData("1starts_with_number")]
    [InlineData("has-hyphen")]
    [InlineData("has space")]
    public void Create_WithInvalidFieldKeyFormat_ThrowsCustomFieldException(string fieldKey)
    {
        TenantId tenantId = TenantId.New();

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "Invoice", fieldKey, "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*lowercase alphanumeric*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDisplayName_ThrowsCustomFieldException(string displayName)
    {
        TenantId tenantId = TenantId.New();

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", displayName, CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Display name is required*");
    }

    [Fact]
    public void Create_WithDisplayNameTooLong_ThrowsCustomFieldException()
    {
        TenantId tenantId = TenantId.New();
        string longName = new string('A', 101);

        Action act = () => CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", longName, CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*100 characters*");
    }

    [Fact]
    public void Create_WithAllFieldTypes_Succeeds()
    {
        TenantId tenantId = TenantId.New();
        Guid createdBy = Guid.NewGuid();

        foreach (CustomFieldType fieldType in Enum.GetValues<CustomFieldType>())
        {
            CustomFieldDefinition definition = CustomFieldDefinition.Create(
                tenantId, "Invoice", $"field_{(int)fieldType}", "Display", fieldType, createdBy, TimeProvider.System);

            definition.FieldType.Should().Be(fieldType);
        }
    }
}

public class CustomFieldDefinitionUpdateTests
{
    private CustomFieldDefinition CreateValidDefinition()
    {
        return CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "po_number", "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
    }

    [Fact]
    public void UpdateDisplayName_WithValidName_ChangesName()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        Guid updatedBy = Guid.NewGuid();

        definition.UpdateDisplayName("Updated Name", updatedBy, TimeProvider.System);

        definition.DisplayName.Should().Be("Updated Name");
        definition.UpdatedBy.Should().Be(updatedBy);
        definition.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithEmptyName_ThrowsCustomFieldException(string name)
    {
        CustomFieldDefinition definition = CreateValidDefinition();

        Action act = () => definition.UpdateDisplayName(name, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Display name is required*");
    }

    [Fact]
    public void UpdateDisplayName_WithNameTooLong_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateValidDefinition();

        Action act = () => definition.UpdateDisplayName(new string('X', 101), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*100 characters*");
    }

    [Fact]
    public void UpdateDescription_WithValue_SetsDescription()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        Guid updatedBy = Guid.NewGuid();

        definition.UpdateDescription("A helpful description", updatedBy, TimeProvider.System);

        definition.Description.Should().Be("A helpful description");
        definition.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void UpdateDescription_WithNull_ClearsDescription()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.UpdateDescription("something", Guid.NewGuid(), TimeProvider.System);

        definition.UpdateDescription(null, Guid.NewGuid(), TimeProvider.System);

        definition.Description.Should().BeNull();
    }

    [Fact]
    public void SetRequired_ToTrue_SetsIsRequired()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        Guid updatedBy = Guid.NewGuid();

        definition.SetRequired(true, updatedBy, TimeProvider.System);

        definition.IsRequired.Should().BeTrue();
        definition.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SetRequired_ToFalse_ClearsIsRequired()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.SetRequired(true, Guid.NewGuid(), TimeProvider.System);

        definition.SetRequired(false, Guid.NewGuid(), TimeProvider.System);

        definition.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void SetDisplayOrder_WithValidOrder_SetsOrder()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        Guid updatedBy = Guid.NewGuid();

        definition.SetDisplayOrder(5, updatedBy, TimeProvider.System);

        definition.DisplayOrder.Should().Be(5);
        definition.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SetDisplayOrder_WithZero_SetsOrderToZero()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.SetDisplayOrder(10, Guid.NewGuid(), TimeProvider.System);

        definition.SetDisplayOrder(0, Guid.NewGuid(), TimeProvider.System);

        definition.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void SetDisplayOrder_WithNegativeOrder_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateValidDefinition();

        Action act = () => definition.SetDisplayOrder(-1, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Display order cannot be negative*");
    }
}

public class CustomFieldDefinitionValidationRulesTests
{
    private CustomFieldDefinition CreateDefinition(CustomFieldType fieldType)
    {
        string fieldKey = $"field_{(int)fieldType}";
        return CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, "Test Field", fieldType, Guid.NewGuid(), TimeProvider.System);
    }

    [Fact]
    public void SetValidationRules_WithNull_ClearsRules()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        definition.SetValidationRules(new FieldValidationRules { MaxLength = 50 }, Guid.NewGuid(), TimeProvider.System);

        definition.SetValidationRules(null, Guid.NewGuid(), TimeProvider.System);

        definition.ValidationRulesJson.Should().BeNull();
        definition.GetValidationRules().Should().BeNull();
    }

    [Fact]
    public void SetValidationRules_ForTextField_WithStringLengthRules_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new FieldValidationRules { MinLength = 5, MaxLength = 50 };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        FieldValidationRules? stored = definition.GetValidationRules();
        stored.Should().NotBeNull();
        stored.MinLength.Should().Be(5);
        stored.MaxLength.Should().Be(50);
    }

    [Theory]
    [InlineData(CustomFieldType.TextArea)]
    [InlineData(CustomFieldType.Email)]
    [InlineData(CustomFieldType.Url)]
    [InlineData(CustomFieldType.Phone)]
    public void SetValidationRules_ForTextBasedTypes_WithStringLengthRules_Succeeds(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { MaxLength = 100 };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        definition.GetValidationRules()!.MaxLength.Should().Be(100);
    }

    [Theory]
    [InlineData(CustomFieldType.Number)]
    [InlineData(CustomFieldType.Decimal)]
    [InlineData(CustomFieldType.Date)]
    [InlineData(CustomFieldType.DateTime)]
    [InlineData(CustomFieldType.Boolean)]
    [InlineData(CustomFieldType.Dropdown)]
    [InlineData(CustomFieldType.MultiSelect)]
    public void SetValidationRules_ForNonTextTypes_WithStringLengthRules_ThrowsCustomFieldException(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { MinLength = 5 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*MinLength/MaxLength*text-based*");
    }

    [Theory]
    [InlineData(CustomFieldType.Number)]
    [InlineData(CustomFieldType.Decimal)]
    public void SetValidationRules_ForNumericTypes_WithMinMaxRules_Succeeds(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { Min = 0, Max = 1000 };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        FieldValidationRules? stored = definition.GetValidationRules();
        stored!.Min.Should().Be(0);
        stored.Max.Should().Be(1000);
    }

    [Theory]
    [InlineData(CustomFieldType.Text)]
    [InlineData(CustomFieldType.Boolean)]
    [InlineData(CustomFieldType.Date)]
    [InlineData(CustomFieldType.Dropdown)]
    public void SetValidationRules_ForNonNumericTypes_WithMinMaxRules_ThrowsCustomFieldException(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { Min = 0 };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Min/Max*numeric*");
    }

    [Theory]
    [InlineData(CustomFieldType.Date)]
    [InlineData(CustomFieldType.DateTime)]
    public void SetValidationRules_ForDateTypes_WithDateRangeRules_Succeeds(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules
        {
            MinDate = new DateTime(2020, 1, 1),
            MaxDate = new DateTime(2030, 12, 31)
        };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        FieldValidationRules? stored = definition.GetValidationRules();
        stored!.MinDate.Should().Be(new DateTime(2020, 1, 1));
        stored.MaxDate.Should().Be(new DateTime(2030, 12, 31));
    }

    [Theory]
    [InlineData(CustomFieldType.Text)]
    [InlineData(CustomFieldType.Number)]
    [InlineData(CustomFieldType.Boolean)]
    public void SetValidationRules_ForNonDateTypes_WithDateRangeRules_ThrowsCustomFieldException(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { MinDate = DateTime.UtcNow };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*MinDate/MaxDate*date*");
    }

    [Fact]
    public void SetValidationRules_ForTextType_WithPattern_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new FieldValidationRules { Pattern = @"^\d{3}-\d{4}$" };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        definition.GetValidationRules()!.Pattern.Should().Be(@"^\d{3}-\d{4}$");
    }

    [Fact]
    public void SetValidationRules_ForTextAreaType_WithPattern_Succeeds()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.TextArea);
        FieldValidationRules rules = new FieldValidationRules { Pattern = @"^[A-Z]+$" };

        definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        definition.GetValidationRules()!.Pattern.Should().Be(@"^[A-Z]+$");
    }

    [Theory]
    [InlineData(CustomFieldType.Number)]
    [InlineData(CustomFieldType.Date)]
    [InlineData(CustomFieldType.Dropdown)]
    public void SetValidationRules_ForNonTextTypes_WithPattern_ThrowsCustomFieldException(CustomFieldType fieldType)
    {
        CustomFieldDefinition definition = CreateDefinition(fieldType);
        FieldValidationRules rules = new FieldValidationRules { Pattern = @"^\d+$" };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Pattern*text*");
    }

    [Fact]
    public void SetValidationRules_WithInvalidRegex_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);
        FieldValidationRules rules = new FieldValidationRules { Pattern = "[invalid" };

        Action act = () => definition.SetValidationRules(rules, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Invalid regex pattern*");
    }

    [Fact]
    public void GetValidationRules_WhenNotSet_ReturnsNull()
    {
        CustomFieldDefinition definition = CreateDefinition(CustomFieldType.Text);

        FieldValidationRules? rules = definition.GetValidationRules();

        rules.Should().BeNull();
    }
}

public class CustomFieldDefinitionOptionsTests
{
    private CustomFieldDefinition CreateDropdownDefinition()
    {
        return CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "status_field", "Status", CustomFieldType.Dropdown, Guid.NewGuid(), TimeProvider.System);
    }

    private CustomFieldDefinition CreateMultiSelectDefinition()
    {
        return CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "tags_field", "Tags", CustomFieldType.MultiSelect, Guid.NewGuid(), TimeProvider.System);
    }

    [Fact]
    public void SetOptions_ForDropdownType_SetsOptions()
    {
        CustomFieldDefinition definition = CreateDropdownDefinition();
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "draft", Label = "Draft", Order = 1 },
            new CustomFieldOption { Value = "published", Label = "Published", Order = 2 }
        };

        definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        IReadOnlyList<CustomFieldOption> stored = definition.GetOptions();
        stored.Should().HaveCount(2);
        stored[0].Value.Should().Be("draft");
        stored[1].Value.Should().Be("published");
    }

    [Fact]
    public void SetOptions_ForMultiSelectType_SetsOptions()
    {
        CustomFieldDefinition definition = CreateMultiSelectDefinition();
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "red", Label = "Red" },
            new CustomFieldOption { Value = "blue", Label = "Blue" }
        };

        definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        definition.GetOptions().Should().HaveCount(2);
    }

    [Fact]
    public void SetOptions_ForNonDropdownType_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "text_field", "Text", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "a", Label = "A" }
        };

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Options are only allowed for Dropdown and MultiSelect*");
    }

    [Fact]
    public void SetOptions_WithDuplicateValues_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateDropdownDefinition();
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "draft", Label = "Draft 1" },
            new CustomFieldOption { Value = "draft", Label = "Draft 2" }
        };

        Action act = () => definition.SetOptions(options, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*Duplicate option values*");
    }

    [Fact]
    public void SetOptions_WithNull_ClearsOptions()
    {
        CustomFieldDefinition definition = CreateDropdownDefinition();
        definition.SetOptions(new List<CustomFieldOption>
        {
            new CustomFieldOption { Value = "a", Label = "A" }
        }, Guid.NewGuid(), TimeProvider.System);

        definition.SetOptions(null, Guid.NewGuid(), TimeProvider.System);

        definition.GetOptions().Should().BeEmpty();
    }

    [Fact]
    public void SetOptions_WithEmptyList_ClearsOptions()
    {
        CustomFieldDefinition definition = CreateDropdownDefinition();
        definition.SetOptions(new List<CustomFieldOption>
        {
            new CustomFieldOption { Value = "a", Label = "A" }
        }, Guid.NewGuid(), TimeProvider.System);

        definition.SetOptions(new List<CustomFieldOption>(), Guid.NewGuid(), TimeProvider.System);

        definition.GetOptions().Should().BeEmpty();
    }

    [Fact]
    public void GetOptions_WhenNotSet_ReturnsEmptyList()
    {
        CustomFieldDefinition definition = CreateDropdownDefinition();

        IReadOnlyList<CustomFieldOption> options = definition.GetOptions();

        options.Should().BeEmpty();
    }
}

public class CustomFieldDefinitionLifecycleTests
{
    private CustomFieldDefinition CreateValidDefinition()
    {
        return CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "po_number", "PO Number", CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactiveAndRaisesEvent()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.ClearDomainEvents();
        Guid deactivatedBy = Guid.NewGuid();

        definition.Deactivate(deactivatedBy, TimeProvider.System);

        definition.IsActive.Should().BeFalse();
        definition.UpdatedBy.Should().Be(deactivatedBy);
        definition.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomFieldDefinitionDeactivatedEvent>();
    }

    [Fact]
    public void Deactivate_WhenActive_EventHasCorrectProperties()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.ClearDomainEvents();

        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        CustomFieldDefinitionDeactivatedEvent evt = definition.DomainEvents
            .OfType<CustomFieldDefinitionDeactivatedEvent>().Single();
        evt.DefinitionId.Should().Be(definition.Id.Value);
        evt.TenantId.Should().Be(definition.TenantId.Value);
        evt.EntityType.Should().Be("Invoice");
        evt.FieldKey.Should().Be("po_number");
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        Action act = () => definition.Deactivate(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*already deactivated*");
    }

    [Fact]
    public void Activate_WhenInactive_SetsActive()
    {
        CustomFieldDefinition definition = CreateValidDefinition();
        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);
        Guid activatedBy = Guid.NewGuid();

        definition.Activate(activatedBy, TimeProvider.System);

        definition.IsActive.Should().BeTrue();
        definition.UpdatedBy.Should().Be(activatedBy);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateValidDefinition();

        Action act = () => definition.Activate(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<CustomFieldException>()
            .WithMessage("*already active*");
    }
}
