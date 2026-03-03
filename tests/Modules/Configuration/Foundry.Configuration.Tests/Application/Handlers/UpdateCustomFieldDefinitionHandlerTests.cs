using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class UpdateCustomFieldDefinitionHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly UpdateCustomFieldDefinitionHandler _handler;

    public UpdateCustomFieldDefinitionHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(Guid.NewGuid());
        _handler = new UpdateCustomFieldDefinitionHandler(_repository, currentUserService, TimeProvider.System);
    }

    private static CustomFieldDefinition CreateDefinition(string entityType = "Invoice", string fieldKey = "test_field")
    {
        TenantId tenantId = TenantId.New();
        return CustomFieldDefinition.Create(tenantId, entityType, fieldKey, "Test Field", CustomFieldType.Text, Guid.Empty, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidDisplayName_UpdatesAndReturnsDto()
    {
        CustomFieldDefinition definition = CreateDefinition();
        Guid id = definition.Id.Value;

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinition command = new(Id: id, DisplayName: "Updated Name");

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayName.Should().Be("Updated Name");
        await _repository.Received(1).UpdateAsync(definition, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDescription_UpdatesDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinition command = new(Id: definition.Id.Value, Description: "New description");

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Description.Should().Be("New description");
    }

    [Fact]
    public async Task Handle_WithIsRequired_UpdatesRequired()
    {
        CustomFieldDefinition definition = CreateDefinition();

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinition command = new(Id: definition.Id.Value, IsRequired: true);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDisplayOrder_UpdatesOrder()
    {
        CustomFieldDefinition definition = CreateDefinition();

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinition command = new(Id: definition.Id.Value, DisplayOrder: 5);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithValidationRules_UpdatesRules()
    {
        CustomFieldDefinition definition = CreateDefinition();

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        FieldValidationRules rules = new() { MaxLength = 100 };
        UpdateCustomFieldDefinition command = new(Id: definition.Id.Value, ValidationRules: rules);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.ValidationRules.Should().NotBeNull();
        result.ValidationRules!.MaxLength.Should().Be(100);
    }

    [Fact]
    public async Task Handle_WithOptions_UpdatesOptions()
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "dept_select", "Department", CustomFieldType.Dropdown, Guid.Empty, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        List<CustomFieldOption> options =
        [
            new CustomFieldOption { Value = "a", Label = "Option A" },
            new CustomFieldOption { Value = "b", Label = "Option B" }
        ];
        UpdateCustomFieldDefinition command = new(Id: definition.Id.Value, Options: options);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsCustomFieldException()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        UpdateCustomFieldDefinition command = new(Id: Guid.NewGuid(), DisplayName: "Test");

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*not found*");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMultipleUpdates_AppliesAll()
    {
        CustomFieldDefinition definition = CreateDefinition();

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinition command = new(
            Id: definition.Id.Value,
            DisplayName: "New Name",
            Description: "New Desc",
            IsRequired: true,
            DisplayOrder: 3);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayName.Should().Be("New Name");
        result.Description.Should().Be("New Desc");
        result.IsRequired.Should().BeTrue();
        result.DisplayOrder.Should().Be(3);
    }
}
