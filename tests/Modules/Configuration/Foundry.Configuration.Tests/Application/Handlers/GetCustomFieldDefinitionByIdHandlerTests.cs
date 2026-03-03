using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Application.Queries;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetCustomFieldDefinitionByIdHandlerTests
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly GetCustomFieldDefinitionByIdHandler _handler;

    public GetCustomFieldDefinitionByIdHandlerTests()
    {
        _repository = Substitute.For<ICustomFieldDefinitionRepository>();
        _handler = new GetCustomFieldDefinitionByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenFound_ReturnsDto()
    {
        TenantId tenantId = TenantId.New();
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Text, Guid.Empty);

        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        GetCustomFieldDefinitionById query = new(definition.Id.Value);

        CustomFieldDefinitionDto? result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.FieldKey.Should().Be("po_number");
        result.DisplayName.Should().Be("PO Number");
        result.EntityType.Should().Be("Invoice");
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        GetCustomFieldDefinitionById query = new(Guid.NewGuid());

        CustomFieldDefinitionDto? result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }
}
