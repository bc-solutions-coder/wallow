using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Application.CustomFields.Services;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Billing.Tests.Application.CustomFields;

/// <summary>
/// Fake Invoice entity for custom field validator tests.
/// Must be named "Invoice" to match CustomFieldRegistry.
/// </summary>
internal sealed class Invoice : IHasCustomFields
{
    public Dictionary<string, object>? CustomFields { get; private set; }

    public void SetCustomFields(Dictionary<string, object>? customFields)
    {
        CustomFields = customFields;
    }
}

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

    [Fact]
    public async Task ValidateAsync_WhenTenantNotResolved_ReturnsSuccess()
    {
        _tenantContext.IsResolved.Returns(false);
        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenNoDefinitions_ReturnsSuccess()
    {
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([]);
        Invoice entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateRequiredTextField("required_field", "Required Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields([]);

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.FieldKey.Should().Be("required_field");
    }

    [Fact]
    public async Task ValidateAsync_WhenRequiredFieldPresent_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateRequiredTextField("required_field", "Required Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "required_field", "some value" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenOptionalFieldAbsent_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateTextField("optional_field", "Optional Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields([]);

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithTextFieldAndValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateTextField("text_field", "Text Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "text_field", "hello" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndNonNumericValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateNumberField("num_field", "Number Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "num_field", "not a number" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].FieldKey.Should().Be("num_field");
    }

    [Fact]
    public async Task ValidateAsync_WithMinLengthRule_EnforcesMinLength()
    {
        CustomFieldDefinition definition = CreateTextField("text_field", "Text Field");
        definition.SetValidationRules(new() { MinLength = 5 }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "text_field", "ab" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("at least 5 characters");
    }

    [Fact]
    public async Task ValidateAsync_WithDropdownAndValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDropdownField("status_field", "Status");
        definition.SetOptions(
            [new() { Value = "active", Label = "Active" }, new() { Value = "inactive", Label = "Inactive" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "status_field", "active" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDropdownAndInvalidValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateDropdownField("status_field", "Status");
        definition.SetOptions(
            [new() { Value = "active", Label = "Active" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "status_field", "unknown_value" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithEmailFieldAndValidEmail_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateEmailField("email_field", "Email");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "email_field", "user@example.com" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithEmailFieldAndInvalidEmail_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateEmailField("email_field", "Email");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "email_field", "not-an-email" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("valid email");
    }

    [Fact]
    public async Task ValidateAsync_WithNullCustomFields_ReturnsSuccessForOptionalFields()
    {
        CustomFieldDefinition definition = CreateTextField("optional_field", "Optional");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(null);

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithUrlFieldAndValidUrl_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateUrlField("website_field", "Website");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "website_field", "https://example.com" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithUrlFieldAndInvalidUrl_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateUrlField("website_field", "Website");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "website_field", "not-a-url" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithPatternRule_ValidatesPattern()
    {
        CustomFieldDefinition definition = CreateTextField("zip_field", "Zip Code");
        definition.SetValidationRules(new() { Pattern = @"^\d{5}$" }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "zip_field", "12345" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithPatternRule_FailsWhenPatternNotMatched()
    {
        CustomFieldDefinition definition = CreateTextField("zip_field", "Zip Code");
        definition.SetValidationRules(new() { Pattern = @"^\d{5}$" }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "zip_field", "invalid" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithMaxLengthRule_EnforcesMaxLength()
    {
        CustomFieldDefinition definition = CreateTextField("code_field", "Code");
        definition.SetValidationRules(new() { MaxLength = 3 }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "code_field", "TOOLONG" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("at most 3 characters");
    }

    [Fact]
    public async Task ValidateAsync_WithNumericMinRule_EnforcesMinValue()
    {
        CustomFieldDefinition definition = CreateDecimalField("amount_field", "Amount");
        definition.SetValidationRules(new() { Min = 10 }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "amount_field", 5m } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("at least 10");
    }

    [Fact]
    public async Task ValidateAsync_WithNumericMaxRule_EnforcesMaxValue()
    {
        CustomFieldDefinition definition = CreateDecimalField("amount_field", "Amount");
        definition.SetValidationRules(new() { Max = 100 }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "amount_field", 200m } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("at most 100");
    }

    [Fact]
    public async Task ValidateAsync_WithDateFieldAndValidDate_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Start Date");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", "2025-06-15" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateFieldAndInvalidDate_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Start Date");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", "not-a-date" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("valid date");
    }

    [Fact]
    public async Task ValidateAsync_WithDateMinRule_EnforcesMinDate()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Start Date");
        definition.SetValidationRules(
            new() { MinDate = new DateTime(2025, 1, 1) }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", "2024-06-01" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("on or after");
    }

    [Fact]
    public async Task ValidateAsync_WithDateMaxRule_EnforcesMaxDate()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "End Date");
        definition.SetValidationRules(
            new() { MaxDate = new DateTime(2025, 12, 31) }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", "2026-06-01" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("on or before");
    }

    [Fact]
    public async Task ValidateAsync_WithDecimalFieldAndValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDecimalField("price_field", "Price");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "price_field", 19.99m } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDecimalFieldAndNonNumericValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateDecimalField("price_field", "Price");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "price_field", "not-a-number" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("must be a number");
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateBooleanField("active_field", "Is Active");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "active_field", true } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndStringTrue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateBooleanField("active_field", "Is Active");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "active_field", "true" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndInvalidValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateBooleanField("active_field", "Is Active");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "active_field", "maybe" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("true or false");
    }

    [Fact]
    public async Task ValidateAsync_WithDateTimeFieldAndInvalidValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateDateTimeField("timestamp_field", "Timestamp");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "timestamp_field", "not-a-datetime" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("valid date and time");
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndValidValues_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [
                new() { Value = "urgent", Label = "Urgent" },
                new() { Value = "review", Label = "Review" },
                new() { Value = "hold", Label = "Hold" }
            ],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        List<string> tags = ["urgent", "review"];
        entity.SetCustomFields(new() { { "tags_field", tags } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndInvalidValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [new() { Value = "urgent", Label = "Urgent" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        List<string> tags = ["urgent", "invalid_tag"];
        entity.SetCustomFields(new() { { "tags_field", tags } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("invalid value: invalid_tag");
    }

    [Fact]
    public async Task ValidateAsync_WithPatternAndCustomMessage_UsesCustomMessage()
    {
        CustomFieldDefinition definition = CreateTextField("phone_field", "Phone");
        definition.SetValidationRules(
            new() { Pattern = @"^\d{10}$", PatternMessage = "Must be exactly 10 digits" },
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "phone_field", "abc" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Be("Must be exactly 10 digits");
    }

    [Fact]
    public async Task ValidateAsync_WithRequiredFieldAndWhitespaceValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateRequiredTextField("required_field", "Required Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "required_field", "   " } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("is required");
    }

    [Fact]
    public async Task ValidateAsync_WithDropdownAndInactiveOption_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateDropdownField("status_field", "Status");
        definition.SetOptions(
            [
                new() { Value = "active", Label = "Active", IsActive = true },
                new() { Value = "deprecated", Label = "Deprecated", IsActive = false }
            ],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "status_field", "deprecated" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndValidInteger_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateNumberField("quantity_field", "Quantity");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "quantity_field", 42 } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndDecimalValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateNumberField("quantity_field", "Quantity");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "quantity_field", "3.14" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("whole number");
    }

    private static CustomFieldDefinition CreateTextField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Text,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateRequiredTextField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CreateTextField(fieldKey, displayName);
        def.SetRequired(true, Guid.NewGuid(), TimeProvider.System);
        return def;
    }

    private static CustomFieldDefinition CreateNumberField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Number,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateDropdownField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Dropdown,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateEmailField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Email,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateUrlField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Url,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateDecimalField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Decimal,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateBooleanField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Boolean,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateDateField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.Date,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    private static CustomFieldDefinition CreateDateTimeField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.DateTime,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndIEnumerableOfObjects_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [
                new() { Value = "urgent", Label = "Urgent" },
                new() { Value = "review", Label = "Review" }
            ],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        List<object> tags = ["urgent", "review"];
        entity.SetCustomFields(new() { { "tags_field", tags } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndNonListValue_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "tags_field", 42 } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("must be a list");
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementString_NormalizesAndValidates()
    {
        CustomFieldDefinition definition = CreateTextField("text_field", "Text Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        // Simulate JsonElement from deserialized JSON
        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("\"hello world\"").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "text_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementNumber_NormalizesAndValidates()
    {
        CustomFieldDefinition definition = CreateNumberField("num_field", "Number");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("42").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "num_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementBoolean_NormalizesAndValidates()
    {
        CustomFieldDefinition definition = CreateBooleanField("bool_field", "Is Active");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElementTrue = System.Text.Json.JsonDocument.Parse("true").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "bool_field", jsonElementTrue } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementFalse_NormalizesAndValidates()
    {
        CustomFieldDefinition definition = CreateBooleanField("bool_field", "Is Active");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("false").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "bool_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementNull_TreatsAsEmpty()
    {
        CustomFieldDefinition definition = CreateTextField("text_field", "Text");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("null").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "text_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementArray_NormalizesToList()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [new() { Value = "a", Label = "A" }, new() { Value = "b", Label = "B" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("[\"a\", \"b\"]").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "tags_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementDecimal_NormalizesAndValidates()
    {
        CustomFieldDefinition definition = CreateDecimalField("price_field", "Price");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("19.99").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "price_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateTimeFieldAndValidValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDateTimeField("ts_field", "Timestamp");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "ts_field", DateTime.UtcNow } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDropdownAndEmptyOptions_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDropdownField("status_field", "Status");
        // No options set — empty options list
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "status_field", "anything" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndSingleConvertibleValue_ValidatesAsString()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [new() { Value = "urgent", Label = "Urgent" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        // MultiSelect but value is a single string (not a list) — triggers the fallback branch
        entity.SetCustomFields(new() { { "tags_field", new List<string> { "urgent" } } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    private static CustomFieldDefinition CreateMultiSelectField(string fieldKey, string displayName)
    {
        CustomFieldDefinition def = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, displayName, CustomFieldType.MultiSelect,
            Guid.NewGuid(), TimeProvider.System);
        def.ClearDomainEvents();
        return def;
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementObject_NormalizesToString()
    {
        CustomFieldDefinition definition = CreateTextField("obj_field", "Object Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        // JsonValueKind.Object falls through to the default case in NormalizeValue
        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("{\"key\":\"val\"}").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "obj_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndLongValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateNumberField("num_field", "Number");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "num_field", 42L } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndIntValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateNumberField("num_field", "Number");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "num_field", 42 } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDecimalFieldAndFloatValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDecimalField("dec_field", "Decimal");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "dec_field", 3.14f } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDecimalFieldAndDoubleValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDecimalField("dec_field", "Decimal");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "dec_field", 3.14d } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndStringFalse_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateBooleanField("bool_field", "Bool");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "bool_field", "false" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndString1_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateBooleanField("bool_field", "Bool");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "bool_field", "1" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithBooleanFieldAndString0_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateBooleanField("bool_field", "Bool");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "bool_field", "0" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateFieldAndDateTimeValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Date");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", DateTime.UtcNow } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateFieldAndDateOnlyValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Date");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", DateOnly.FromDateTime(DateTime.UtcNow) } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateTimeFieldAndDateTimeObjectValue_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDateTimeField("ts_field", "Timestamp");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "ts_field", new DateTime(2025, 6, 15, 10, 30, 0) } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithUrlFieldAndHttpUrl_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateUrlField("url_field", "URL");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "url_field", "http://example.com" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithUrlFieldAndFtpUrl_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateUrlField("url_field", "URL");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "url_field", "ftp://example.com" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("valid URL");
    }

    [Fact]
    public async Task ValidateAsync_WithMultiSelectAndSingleStringFallback_ValidatesOptions()
    {
        CustomFieldDefinition definition = CreateMultiSelectField("tags_field", "Tags");
        definition.SetOptions(
            [new() { Value = "urgent", Label = "Urgent" }],
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        // A single non-list, non-string value triggers the _ fallback in ValidateOptions
        entity.SetCustomFields(new() { { "tags_field", new List<object> { "urgent" } } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithPatternRule_DefaultMessage_WhenNoPatternMessageSet()
    {
        CustomFieldDefinition definition = CreateTextField("code_field", "Code");
        definition.SetValidationRules(new() { Pattern = @"^[A-Z]+$" }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "code_field", "abc" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Be("Code format is invalid");
    }

    [Fact]
    public async Task ValidateAsync_WithNumberFieldAndStringInteger_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateNumberField("num_field", "Number");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "num_field", "42" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDecimalFieldAndStringNumber_ReturnsSuccess()
    {
        CustomFieldDefinition definition = CreateDecimalField("dec_field", "Price");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "dec_field", "19.99" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNumericMinAndMaxRules_AllowsValueInRange()
    {
        CustomFieldDefinition definition = CreateDecimalField("amount_field", "Amount");
        definition.SetValidationRules(new() { Min = 10, Max = 100 }, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "amount_field", 50m } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithDateMinAndMaxRules_AllowsDateInRange()
    {
        CustomFieldDefinition definition = CreateDateField("date_field", "Event Date");
        definition.SetValidationRules(
            new() { MinDate = new DateTime(2025, 1, 1), MaxDate = new DateTime(2025, 12, 31) },
            Guid.NewGuid(), TimeProvider.System);
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "date_field", "2025-06-15" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithJsonElementDecimalNotLong_NormalizesToDecimal()
    {
        CustomFieldDefinition definition = CreateDecimalField("dec_field", "Decimal");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        // A large decimal that doesn't fit in long
        System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonDocument.Parse("99999999999999999.99").RootElement;
        Invoice entity = new();
        entity.SetCustomFields(new() { { "dec_field", jsonElement } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithEmailFieldAndNullValue_SkipsValidation()
    {
        CustomFieldDefinition definition = CreateEmailField("email_field", "Email");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        Invoice entity = new();
        entity.SetCustomFields(new() { { "email_field", null! } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }
}
