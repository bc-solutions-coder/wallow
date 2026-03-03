using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class CreateCustomFieldDefinitionHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly CreateCustomFieldDefinitionHandler _handler;

    public CreateCustomFieldDefinitionHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(Guid.NewGuid());
        _handler = new CreateCustomFieldDefinitionHandler(_repository, tenantContext, currentUserService, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesDefinitionAndReturnsDto()
    {
        CreateCustomFieldDefinition command = new(
            EntityType: "Invoice",
            FieldKey: "po_number",
            DisplayName: "PO Number",
            FieldType: CustomFieldType.Text);

        _repository.FieldKeyExistsAsync("Invoice", "po_number", Arg.Any<CancellationToken>())
            .Returns(false);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.EntityType.Should().Be("Invoice");
        result.FieldKey.Should().Be("po_number");
        result.DisplayName.Should().Be("PO Number");
        result.FieldType.Should().Be(CustomFieldType.Text);
        result.IsActive.Should().BeTrue();
        result.IsRequired.Should().BeFalse();

        await _repository.Received(1).AddAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateFieldKey_ThrowsCustomFieldException()
    {
        CreateCustomFieldDefinition command = new(
            EntityType: "Invoice",
            FieldKey: "po_number",
            DisplayName: "PO Number",
            FieldType: CustomFieldType.Text);

        _repository.FieldKeyExistsAsync("Invoice", "po_number", Arg.Any<CancellationToken>())
            .Returns(true);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*already exists*");
        await _repository.DidNotReceive().AddAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDescription_SetsDescription()
    {
        CreateCustomFieldDefinition command = new(
            EntityType: "Invoice",
            FieldKey: "department",
            DisplayName: "Department",
            FieldType: CustomFieldType.Text,
            Description: "The department for this invoice");

        _repository.FieldKeyExistsAsync("Invoice", "department", Arg.Any<CancellationToken>())
            .Returns(false);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Description.Should().Be("The department for this invoice");
    }

    [Fact]
    public async Task Handle_WithIsRequired_SetsRequired()
    {
        CreateCustomFieldDefinition command = new(
            EntityType: "Payment",
            FieldKey: "reference_id",
            DisplayName: "Reference ID",
            FieldType: CustomFieldType.Text,
            IsRequired: true);

        _repository.FieldKeyExistsAsync("Payment", "reference_id", Arg.Any<CancellationToken>())
            .Returns(false);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidationRules_SetsRules()
    {
        FieldValidationRules rules = new() { MaxLength = 50 };

        CreateCustomFieldDefinition command = new(
            EntityType: "Invoice",
            FieldKey: "notes_field",
            DisplayName: "Notes",
            FieldType: CustomFieldType.Text,
            ValidationRules: rules);

        _repository.FieldKeyExistsAsync("Invoice", "notes_field", Arg.Any<CancellationToken>())
            .Returns(false);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.ValidationRules.Should().NotBeNull();
        result.ValidationRules!.MaxLength.Should().Be(50);
    }

    [Fact]
    public async Task Handle_WithOptions_SetsOptionsForDropdown()
    {
        List<CustomFieldOption> options =
        [
            new CustomFieldOption { Value = "eng", Label = "Engineering" },
            new CustomFieldOption { Value = "sales", Label = "Sales" }
        ];

        CreateCustomFieldDefinition command = new(
            EntityType: "Invoice",
            FieldKey: "dept_select",
            DisplayName: "Department",
            FieldType: CustomFieldType.Dropdown,
            Options: options);

        _repository.FieldKeyExistsAsync("Invoice", "dept_select", Arg.Any<CancellationToken>())
            .Returns(false);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Options.Should().NotBeNull();
        result.Options.Should().HaveCount(2);
    }
}
