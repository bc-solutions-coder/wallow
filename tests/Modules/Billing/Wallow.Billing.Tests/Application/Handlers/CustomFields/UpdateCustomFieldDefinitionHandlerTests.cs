using Wallow.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.CustomFields.Exceptions;
using Wallow.Billing.Domain.CustomFields.Identity;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Services;

namespace Wallow.Billing.Tests.Application.Handlers.CustomFields;

public class UpdateCustomFieldDefinitionHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly UpdateCustomFieldDefinitionHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateCustomFieldDefinitionHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _handler = new UpdateCustomFieldDefinitionHandler(
            _repository, _currentUserService, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenDefinitionExists_ReturnsUpdatedDto()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(
            definition.Id.Value,
            DisplayName: "Updated Name");

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Handle_WhenDefinitionNotFound_ThrowsCustomFieldException()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid());

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_WithDisplayName_UpdatesDisplayName()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, DisplayName: "New Label");

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayName.Should().Be("New Label");
    }

    [Fact]
    public async Task Handle_WithDescription_UpdatesDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, Description: "A helpful description");

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Description.Should().Be("A helpful description");
    }

    [Fact]
    public async Task Handle_WithClearDescription_RemovesDescription()
    {
        CustomFieldDefinition definition = CreateDefinition();
        definition.UpdateDescription("Initial description", Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, ClearDescription: true);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithIsRequired_UpdatesIsRequired()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, IsRequired: true);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDisplayOrder_UpdatesDisplayOrder()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, DisplayOrder: 3);

        CustomFieldDefinitionDto result = await _handler.Handle(command, CancellationToken.None);

        result.DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_CallsRepositoryUpdateAndSave()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value, DisplayName: "Updated");

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(definition, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoChanges_StillCallsUpdateAndSave()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        UpdateCustomFieldDefinitionCommand command = new(definition.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CustomFieldDefinition CreateDefinition()
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", "test_field", "Test Field",
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}
