using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Application.CustomFields.Services;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Tests.Application.CustomFields;

/// <summary>
/// A simple test entity implementing IHasCustomFields for validator tests.
/// </summary>
internal sealed class TestEntity : IHasCustomFields
{
    public string Name => "Invoice";
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
        TestEntity entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenNoDefinitions_ReturnsSuccess()
    {
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([]);
        TestEntity entity = new();

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        CustomFieldDefinition definition = CreateRequiredTextField("required_field", "Required Field");
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns([definition]);

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
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

        TestEntity entity = new();
        entity.SetCustomFields(new() { { "zip_field", "invalid" } });

        CustomFieldValidationResult result = await _validator.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
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
}
