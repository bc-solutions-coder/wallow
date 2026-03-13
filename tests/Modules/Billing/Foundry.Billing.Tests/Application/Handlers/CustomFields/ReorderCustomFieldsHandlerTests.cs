using Foundry.Billing.Application.CustomFields.Commands.ReorderCustomFields;
using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Tests.Application.Handlers.CustomFields;

public class ReorderCustomFieldsHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ReorderCustomFieldsHandler _handler;

    public ReorderCustomFieldsHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _handler = new ReorderCustomFieldsHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidOrder_SetsDisplayOrderCorrectly()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_one");
        CustomFieldDefinition field2 = CreateDefinition("field_two");
        CustomFieldDefinition field3 = CreateDefinition("field_three");

        _repository.GetByEntityTypeAsync("Invoice", includeInactive: true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { field1, field2, field3 });

        IReadOnlyList<Guid> idsInOrder = [field3.Id.Value, field1.Id.Value, field2.Id.Value];
        ReorderCustomFieldsCommand command = new("Invoice", idsInOrder);

        await _handler.Handle(command, CancellationToken.None);

        field3.DisplayOrder.Should().Be(0);
        field1.DisplayOrder.Should().Be(1);
        field2.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithUnknownFieldId_ThrowsCustomFieldException()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_one");
        _repository.GetByEntityTypeAsync("Invoice", includeInactive: true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { field1 });

        Guid unknownId = Guid.NewGuid();
        ReorderCustomFieldsCommand command = new("Invoice", [field1.Id.Value, unknownId]);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*not found for entity type*");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_CallsSaveChanges()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_one");
        CustomFieldDefinition field2 = CreateDefinition("field_two");

        _repository.GetByEntityTypeAsync("Invoice", includeInactive: true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { field1, field2 });

        ReorderCustomFieldsCommand command = new("Invoice", [field1.Id.Value, field2.Id.Value]);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyFieldList_CompletesWithoutUpdates()
    {
        _repository.GetByEntityTypeAsync("Invoice", includeInactive: true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        ReorderCustomFieldsCommand command = new("Invoice", []);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UpdatesEachFieldInOrder()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_one");
        CustomFieldDefinition field2 = CreateDefinition("field_two");

        _repository.GetByEntityTypeAsync("Invoice", includeInactive: true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { field1, field2 });

        ReorderCustomFieldsCommand command = new("Invoice", [field1.Id.Value, field2.Id.Value]);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(2).UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
    }

    private static CustomFieldDefinition CreateDefinition(string fieldKey)
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TenantId.New(), "Invoice", fieldKey, fieldKey.Replace("_", " "),
            CustomFieldType.Text, Guid.NewGuid(), TimeProvider.System);
        definition.ClearDomainEvents();
        return definition;
    }
}
