using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Application.Handlers;

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
    public async Task Handle_WithActiveDefinition_DeactivatesSuccessfully()
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "test_field", "Test Field", CustomFieldType.Text, Guid.Empty, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        DeactivateCustomFieldDefinition command = new(definition.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        definition.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(definition, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsCustomFieldException()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        DeactivateCustomFieldDefinition command = new(Guid.NewGuid());

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*not found*");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<CustomFieldDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyDeactivated_ThrowsCustomFieldException()
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "test_field", "Test Field", CustomFieldType.Text, Guid.Empty, TimeProvider.System);
        definition.Deactivate(Guid.Empty, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        DeactivateCustomFieldDefinition command = new(definition.Id.Value);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomFieldException>()
            .WithMessage("*already deactivated*");
    }
}
