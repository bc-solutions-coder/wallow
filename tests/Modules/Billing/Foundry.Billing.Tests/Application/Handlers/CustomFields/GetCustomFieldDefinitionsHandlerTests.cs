using Foundry.Billing.Application.CustomFields.DTOs;
using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitionById;
using Foundry.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitions;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Tests.Application.Handlers.CustomFields;

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
    public async Task Handle_WithActiveDefinitions_ReturnsMappedDtos()
    {
        List<CustomFieldDefinition> definitions =
        [
            CreateDefinition("field_one"),
            CreateDefinition("field_two")
        ];
        _repository.GetByEntityTypeAsync("Invoice", false, Arg.Any<CancellationToken>())
            .Returns(definitions);

        IReadOnlyList<CustomFieldDefinitionDto> result = await _handler.Handle(
            new GetCustomFieldDefinitionsQuery("Invoice", false), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].FieldKey.Should().Be("field_one");
        result[1].FieldKey.Should().Be("field_two");
    }

    [Fact]
    public async Task Handle_WithIncludeInactive_PassesThroughToRepository()
    {
        _repository.GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.Handle(new GetCustomFieldDefinitionsQuery("Invoice", true), CancellationToken.None);

        await _repository.Received(1).GetByEntityTypeAsync("Invoice", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoDefinitions_ReturnsEmptyList()
    {
        _repository.GetByEntityTypeAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<CustomFieldDefinitionDto> result = await _handler.Handle(
            new GetCustomFieldDefinitionsQuery("Payment", false), CancellationToken.None);

        result.Should().BeEmpty();
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
    public async Task Handle_WhenDefinitionExists_ReturnsMappedDto()
    {
        CustomFieldDefinition definition = CreateDefinition();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        CustomFieldDefinitionDto? result = await _handler.Handle(
            new GetCustomFieldDefinitionByIdQuery(definition.Id.Value), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(definition.Id.Value);
        result.FieldKey.Should().Be("test_field");
    }

    [Fact]
    public async Task Handle_WhenDefinitionNotFound_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        CustomFieldDefinitionDto? result = await _handler.Handle(
            new GetCustomFieldDefinitionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesCorrectIdToRepository()
    {
        Guid id = Guid.NewGuid();
        _repository.GetByIdAsync(Arg.Any<CustomFieldDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinition?)null);

        await _handler.Handle(new GetCustomFieldDefinitionByIdQuery(id), CancellationToken.None);

        await _repository.Received(1).GetByIdAsync(
            Arg.Is<CustomFieldDefinitionId>(x => x.Value == id),
            Arg.Any<CancellationToken>());
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
