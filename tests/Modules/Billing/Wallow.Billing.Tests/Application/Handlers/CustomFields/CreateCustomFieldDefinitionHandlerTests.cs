using Wallow.Billing.Application.CustomFields.Commands.CreateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;

namespace Wallow.Billing.Tests.Application.Handlers.CustomFields;

public class CreateCustomFieldDefinitionHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly CreateCustomFieldDefinitionHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TenantId _tenantId = TenantId.New();

    public CreateCustomFieldDefinitionHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _tenantContext.TenantId.Returns(_tenantId);
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _handler = new CreateCustomFieldDefinitionHandler(
            _repository, _tenantContext, _currentUserService, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessWithDto()
    {
        _repository.FieldKeyExistsAsync("Invoice", "po_number", Arg.Any<CancellationToken>())
            .Returns(false);

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "PO Number", CustomFieldType.Text);

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FieldKey.Should().Be("po_number");
        result.Value.DisplayName.Should().Be("PO Number");
        result.Value.EntityType.Should().Be("Invoice");
        result.Value.FieldType.Should().Be(CustomFieldType.Text);
    }

    [Fact]
    public async Task Handle_WithDuplicateFieldKey_ReturnsConflictError()
    {
        _repository.FieldKeyExistsAsync("Invoice", "existing_field", Arg.Any<CancellationToken>())
            .Returns(true);

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "existing_field", "Existing", CustomFieldType.Text);

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict.Error");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_CallsRepositoryAddAndSave()
    {
        _repository.FieldKeyExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "ref_number", "Reference Number", CustomFieldType.Text);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDescription_SetsDescriptionOnDefinition()
    {
        _repository.FieldKeyExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "notes_field", "Notes", CustomFieldType.TextArea,
            Description: "Internal notes for invoicing");

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Internal notes for invoicing");
    }

    [Fact]
    public async Task Handle_WithIsRequired_SetsIsRequiredOnDefinition()
    {
        _repository.FieldKeyExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "required_field", "Required Field", CustomFieldType.Text,
            IsRequired: true);

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidationRules_SetsRulesOnDefinition()
    {
        _repository.FieldKeyExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        FieldValidationRules rules = new() { MinLength = 5, MaxLength = 100 };

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "text_field", "Text Field", CustomFieldType.Text,
            ValidationRules: rules);

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationRules.Should().NotBeNull();
        result.Value.ValidationRules!.MinLength.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithOptions_SetsOptionsOnDefinition()
    {
        _repository.FieldKeyExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        IReadOnlyList<CustomFieldOption> options =
        [
            new() { Value = "opt1", Label = "Option 1" },
            new() { Value = "opt2", Label = "Option 2" }
        ];

        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "dropdown_field", "Dropdown", CustomFieldType.Dropdown,
            Options: options);

        Result<CustomFieldDefinitionDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Options.Should().HaveCount(2);
    }
}
