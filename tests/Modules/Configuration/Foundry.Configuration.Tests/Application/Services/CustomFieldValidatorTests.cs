using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Services;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Configuration.Tests.Application.Services;

public class CustomFieldValidatorTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly CustomFieldValidator _validator;

    public CustomFieldValidatorTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.IsResolved.Returns(true);
        _validator = new CustomFieldValidator(_repository, _tenantContext);
    }

    private static CustomFieldDefinition CreateDefinition(
        string fieldKey,
        CustomFieldType fieldType = CustomFieldType.Text,
        bool isRequired = false)
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            tenantId, "Invoice", fieldKey, fieldKey, fieldType, Guid.Empty, TimeProvider.System);
        if (isRequired)
        {
            def.SetRequired(true, Guid.Empty, TimeProvider.System);
        }
        return def;
    }

    [Fact]
    public async Task ValidateAsync_WhenTenantNotResolved_ReturnsSuccess()
    {
        _tenantContext.IsResolved.Returns(false);

        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenEntityTypeNotSupported_ReturnsSuccess()
    {
        UnsupportedEntity entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenNoDefinitions_ReturnsSuccess()
    {
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_RequiredFieldMissing_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("po_number", isRequired: true);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.FieldKey.Should().Be("po_number");
    }

    [Fact]
    public async Task ValidateAsync_RequiredFieldPresent_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("po_number", isRequired: true);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["po_number"] = "PO-123" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_OptionalFieldMissing_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("notes");
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_NumberField_WithInvalidValue_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("quantity", CustomFieldType.Number);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["quantity"] = "not-a-number" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("whole number");
    }

    [Fact]
    public async Task ValidateAsync_NumberField_WithValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("quantity", CustomFieldType.Number);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["quantity"] = 42 }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_DecimalField_WithInvalidValue_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("amount", CustomFieldType.Decimal);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["amount"] = "abc" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("number");
    }

    [Fact]
    public async Task ValidateAsync_DateField_WithInvalidValue_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("due_date", CustomFieldType.Date);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["due_date"] = "not-a-date" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("date");
    }

    [Fact]
    public async Task ValidateAsync_DateTimeField_WithInvalidValue_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("event_time", CustomFieldType.DateTime);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["event_time"] = "not-a-datetime" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("date and time");
    }

    [Fact]
    public async Task ValidateAsync_BooleanField_WithInvalidValue_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("is_active", CustomFieldType.Boolean);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["is_active"] = "invalid" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("true or false");
    }

    [Fact]
    public async Task ValidateAsync_BooleanField_WithBoolValue_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("is_active", CustomFieldType.Boolean);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["is_active"] = true }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_EmailField_WithInvalidEmail_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("contact_email", CustomFieldType.Email);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["contact_email"] = "not-an-email" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("email");
    }

    [Fact]
    public async Task ValidateAsync_EmailField_WithValidEmail_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("contact_email", CustomFieldType.Email);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["contact_email"] = "test@example.com" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_UrlField_WithInvalidUrl_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("website", CustomFieldType.Url);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["website"] = "not-a-url" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("URL");
    }

    [Fact]
    public async Task ValidateAsync_UrlField_WithValidUrl_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("website", CustomFieldType.Url);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["website"] = "https://example.com" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_MaxLengthRule_Exceeded_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("notes");
        def.SetValidationRules(new FieldValidationRules { MaxLength = 10 }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["notes"] = "This is a very long string" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("at most 10 characters");
    }

    [Fact]
    public async Task ValidateAsync_MinLengthRule_NotMet_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("code");
        def.SetValidationRules(new FieldValidationRules { MinLength = 5 }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["code"] = "AB" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("at least 5 characters");
    }

    [Fact]
    public async Task ValidateAsync_NumericMinRule_BelowMin_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("quantity", CustomFieldType.Number);
        def.SetValidationRules(new FieldValidationRules { Min = 1 }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["quantity"] = 0 }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("at least 1");
    }

    [Fact]
    public async Task ValidateAsync_NumericMaxRule_AboveMax_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("quantity", CustomFieldType.Number);
        def.SetValidationRules(new FieldValidationRules { Max = 100 }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["quantity"] = 150 }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("at most 100");
    }

    [Fact]
    public async Task ValidateAsync_PatternRule_NoMatch_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("code");
        def.SetValidationRules(new FieldValidationRules { Pattern = @"^[A-Z]{3}-\d{3}$" }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["code"] = "invalid" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("format is invalid");
    }

    [Fact]
    public async Task ValidateAsync_PatternRule_WithCustomMessage_UsesCustomMessage()
    {
        CustomFieldDefinition def = CreateDefinition("code");
        def.SetValidationRules(new FieldValidationRules
        {
            Pattern = @"^[A-Z]{3}$",
            PatternMessage = "Must be 3 uppercase letters"
        }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["code"] = "abc" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Be("Must be 3 uppercase letters");
    }

    [Fact]
    public async Task ValidateAsync_PatternRule_Matches_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("code");
        def.SetValidationRules(new FieldValidationRules { Pattern = @"^[A-Z]{3}-\d{3}$" }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["code"] = "ABC-123" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_DropdownField_InvalidOption_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("status", CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "active", Label = "Active" },
            new() { Value = "inactive", Label = "Inactive" }
        ];
        def.SetOptions(options, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["status"] = "unknown" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("must be one of");
    }

    [Fact]
    public async Task ValidateAsync_DropdownField_ValidOption_ReturnsSuccess()
    {
        CustomFieldDefinition def = CreateDefinition("status", CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "active", Label = "Active" },
            new() { Value = "inactive", Label = "Inactive" }
        ];
        def.SetOptions(options, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["status"] = "active" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_DropdownField_InactiveOption_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("status", CustomFieldType.Dropdown);
        List<CustomFieldOption> options =
        [
            new() { Value = "active", Label = "Active" },
            new() { Value = "archived", Label = "Archived", IsActive = false }
        ];
        def.SetOptions(options, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["status"] = "archived" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_DateRangeRule_BeforeMin_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("due_date", CustomFieldType.Date);
        def.SetValidationRules(new FieldValidationRules { MinDate = new DateTime(2025, 1, 1) }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["due_date"] = "2024-06-01" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("on or after");
    }

    [Fact]
    public async Task ValidateAsync_DateRangeRule_AfterMax_ReturnsError()
    {
        CustomFieldDefinition def = CreateDefinition("due_date", CustomFieldType.Date);
        def.SetValidationRules(new FieldValidationRules { MaxDate = new DateTime(2025, 12, 31) }, Guid.Empty, TimeProvider.System);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["due_date"] = "2026-06-01" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("on or before");
    }

    [Fact]
    public async Task ValidateAsync_NullCustomFields_UsesEmptyDictionary()
    {
        CustomFieldDefinition def = CreateDefinition("notes");
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new() { CustomFields = null };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_MultipleErrors_ReturnsAllErrors()
    {
        CustomFieldDefinition def1 = CreateDefinition("field_a", isRequired: true);
        CustomFieldDefinition def2 = CreateDefinition("field_b", isRequired: true);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def1, def2 });

        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidateAsync_BoolStringValues_AreAccepted()
    {
        CustomFieldDefinition def = CreateDefinition("is_active", CustomFieldType.Boolean);
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def });

        Invoice entity = new()
        {
            CustomFields = new Dictionary<string, object> { ["is_active"] = "true" }
        };

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    // Named "Invoice" to match the CustomFieldRegistry supported entity types
    private sealed class Invoice : IHasCustomFields
    {
        public Dictionary<string, object>? CustomFields { get; set; }
        public void SetCustomFields(Dictionary<string, object>? customFields) => CustomFields = customFields;
    }

    // Named to NOT match any supported entity type
    private sealed class UnsupportedEntity : IHasCustomFields
    {
        public Dictionary<string, object>? CustomFields { get; set; }
        public void SetCustomFields(Dictionary<string, object>? customFields) => CustomFields = customFields;
    }
}
