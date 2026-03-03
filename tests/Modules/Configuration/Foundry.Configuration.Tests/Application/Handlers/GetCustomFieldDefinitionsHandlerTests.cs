using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Application.Queries;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetCustomFieldDefinitionsHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly GetCustomFieldDefinitionsHandler _handler;

    public GetCustomFieldDefinitionsHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _handler = new GetCustomFieldDefinitionsHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithDefinitions_ReturnsDtoList()
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition def1 = CustomFieldDefinition.Create(tenantId, "Invoice", "field_a", "Field A", CustomFieldType.Text, Guid.Empty, TimeProvider.System);
        CustomFieldDefinition def2 = CustomFieldDefinition.Create(tenantId, "Invoice", "field_b", "Field B", CustomFieldType.Number, Guid.Empty, TimeProvider.System);

        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition> { def1, def2 });

        GetCustomFieldDefinitions query = new("Invoice");

        IReadOnlyList<CustomFieldDefinitionDto> result = await _handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].FieldKey.Should().Be("field_a");
        result[1].FieldKey.Should().Be("field_b");
    }

    [Fact]
    public async Task Handle_WithNoDefinitions_ReturnsEmptyList()
    {
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        GetCustomFieldDefinitions query = new("Invoice");

        IReadOnlyList<CustomFieldDefinitionDto> result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithIncludeInactive_PassesFlagToRepository()
    {
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        GetCustomFieldDefinitions query = new("Invoice", IncludeInactive: true);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DefaultIncludeInactive_PassesFalse()
    {
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>());

        GetCustomFieldDefinitions query = new("Invoice");

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>());
    }
}
