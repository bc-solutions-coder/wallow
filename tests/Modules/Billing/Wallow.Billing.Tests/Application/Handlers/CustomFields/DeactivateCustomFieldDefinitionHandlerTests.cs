using Wallow.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.CustomFields.Exceptions;
using Wallow.Billing.Domain.CustomFields.Identity;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Tests.Application.Handlers.CustomFields;

public class DeactivateCustomFieldDefinitionHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly DeactivateCustomFieldDefinitionHandler _handler;

    public DeactivateCustomFieldDefinitionHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _handler = new DeactivateCustomFieldDefinitionHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenDefinitionExists_DeactivatesDefinition()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        await _handler.Handle(new DeactivateCustomFieldDefinitionCommand(definition.Id.Value), CancellationToken.None);

        definition.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenDefinitionNotFound_ThrowsCustomFieldException()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        Func<Task> act = () => _handler.Handle(
            new DeactivateCustomFieldDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_CallsRepositoryUpdateAndSave()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        await _handler.Handle(new DeactivateCustomFieldDefinitionCommand(definition.Id.Value), CancellationToken.None);

        await _repository.Received(1).UpdateAsync(definition, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyDeactivated_ThrowsCustomFieldException()
    {
        CustomFieldDefinition definition = CreateActiveDefinition();
        definition.Deactivate(Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        Func<Task> act = () => _handler.Handle(
            new DeactivateCustomFieldDefinitionCommand(definition.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("Field is already deactivated");
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
