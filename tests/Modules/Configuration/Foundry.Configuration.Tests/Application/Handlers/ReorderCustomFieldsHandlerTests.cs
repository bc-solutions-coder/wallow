using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class ReorderCustomFieldsHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ReorderCustomFieldsHandler _handler;

    public ReorderCustomFieldsHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _handler = new ReorderCustomFieldsHandler(_repository, TimeProvider.System);
    }

    private static CustomFieldDefinition CreateDefinition(string fieldKey)
    {
        TenantId tenantId = TenantId.New();
        return CustomFieldDefinition.Create(tenantId, "Invoice", fieldKey, $"Field {fieldKey}", CustomFieldType.Text, Guid.Empty, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidOrder_ReordersFields()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_a");
        CustomFieldDefinition field2 = CreateDefinition("field_b");
        CustomFieldDefinition field3 = CreateDefinition("field_c");

        List<CustomFieldDefinition> definitions = [field1, field2, field3];
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns(definitions);

        ReorderCustomFields command = new("Invoice", [field3.Id.Value, field1.Id.Value, field2.Id.Value]);

        await _handler.Handle(command, CancellationToken.None);

        field3.DisplayOrder.Should().Be(0);
        field1.DisplayOrder.Should().Be(1);
        field2.DisplayOrder.Should().Be(2);
        await _repository.Received(3).UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidFieldId_ThrowsCustomFieldException()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_a");
        List<CustomFieldDefinition> definitions = [field1];
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns(definitions);

        Guid unknownId = Guid.NewGuid();
        ReorderCustomFields command = new("Invoice", [unknownId]);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage($"*{unknownId}*not found*");
    }

    [Fact]
    public async Task Handle_WithSingleField_SetsOrderToZero()
    {
        CustomFieldDefinition field1 = CreateDefinition("field_a");
        List<CustomFieldDefinition> definitions = [field1];
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns(definitions);

        ReorderCustomFields command = new("Invoice", [field1.Id.Value]);

        await _handler.Handle(command, CancellationToken.None);

        field1.DisplayOrder.Should().Be(0);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyOrder_SavesWithoutUpdates()
    {
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        ReorderCustomFields command = new("Invoice", []);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
