using Foundry.Configuration.Api.Controllers;
using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Application.Queries;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Configuration.Tests.Api.Controllers;

public class CustomFieldsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly CustomFieldsController _controller;

    public CustomFieldsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new CustomFieldsController(_bus);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetEntityTypes

    [Fact]
    public async Task GetEntityTypes_ReturnsOkWithEntityTypes()
    {
        List<EntityTypeDto> entityTypes = new()
        {
            new EntityTypeDto("Invoice", "Billing", "Billing invoices"),
            new EntityTypeDto("Payment", "Billing", "Payment records")
        };
        _bus.InvokeAsync<IReadOnlyList<EntityTypeDto>>(Arg.Any<GetSupportedEntityTypes>(), Arg.Any<CancellationToken>())
            .Returns(entityTypes);

        ActionResult<IReadOnlyList<EntityTypeDto>> result = await _controller.GetEntityTypes(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<EntityTypeDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<EntityTypeDto>>().Subject;
        responses.Should().HaveCount(2);
        responses[0].EntityType.Should().Be("Invoice");
        responses[1].EntityType.Should().Be("Payment");
    }

    [Fact]
    public async Task GetEntityTypes_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<IReadOnlyList<EntityTypeDto>>(Arg.Any<GetSupportedEntityTypes>(), Arg.Any<CancellationToken>())
            .Returns(new List<EntityTypeDto>());

        ActionResult<IReadOnlyList<EntityTypeDto>> result = await _controller.GetEntityTypes(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<EntityTypeDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<EntityTypeDto>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion

    #region GetByEntityType

    [Fact]
    public async Task GetByEntityType_ReturnsOkWithDefinitions()
    {
        List<CustomFieldDefinitionDto> definitions = new()
        {
            CreateDefinitionDto("Invoice", "custom_ref"),
            CreateDefinitionDto("Invoice", "priority")
        };
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(Arg.Any<GetCustomFieldDefinitions>(), Arg.Any<CancellationToken>())
            .Returns(definitions);

        ActionResult<IReadOnlyList<CustomFieldDefinitionDto>> result = await _controller.GetByEntityType("Invoice", false, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<CustomFieldDefinitionDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<CustomFieldDefinitionDto>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByEntityType_PassesCorrectParametersToQuery()
    {
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(Arg.Any<GetCustomFieldDefinitions>(), Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinitionDto>());

        await _controller.GetByEntityType("Payment", true, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
            Arg.Is<GetCustomFieldDefinitions>(q => q.EntityType == "Payment" && q.IncludeInactive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByEntityType_DefaultsIncludeInactiveToFalse()
    {
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(Arg.Any<GetCustomFieldDefinitions>(), Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinitionDto>());

        await _controller.GetByEntityType("Invoice", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
            Arg.Is<GetCustomFieldDefinitions>(q => !q.IncludeInactive),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithDefinition()
    {
        Guid definitionId = Guid.NewGuid();
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "custom_ref", definitionId);
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(Arg.Any<GetCustomFieldDefinitionById>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        ActionResult<CustomFieldDefinitionDto> result = await _controller.GetById(definitionId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CustomFieldDefinitionDto response = ok.Value.Should().BeOfType<CustomFieldDefinitionDto>().Subject;
        response.Id.Should().Be(definitionId);
        response.FieldKey.Should().Be("custom_ref");
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid definitionId = Guid.NewGuid();
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(Arg.Any<GetCustomFieldDefinitionById>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinitionDto?)null);

        ActionResult<CustomFieldDefinitionDto> result = await _controller.GetById(definitionId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid definitionId = Guid.NewGuid();
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "ref", definitionId);
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(Arg.Any<GetCustomFieldDefinitionById>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        await _controller.GetById(definitionId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<CustomFieldDefinitionDto?>(
            Arg.Is<GetCustomFieldDefinitionById>(q => q.Id == definitionId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "custom_ref",
            DisplayName = "Custom Reference",
            FieldType = CustomFieldType.Text
        };
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "custom_ref");
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(Arg.Any<CreateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.ActionName.Should().Be(nameof(CustomFieldsController.GetById));
    }

    [Fact]
    public async Task Create_PassesCorrectFieldsToCommand()
    {
        FieldValidationRules rules = new() { MinLength = 1, MaxLength = 100 };
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "opt1", Label = "Option 1" }
        };
        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "priority",
            DisplayName = "Priority",
            FieldType = CustomFieldType.Dropdown,
            Description = "Priority level",
            IsRequired = true,
            ValidationRules = rules,
            Options = options
        };
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "priority");
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(Arg.Any<CreateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<CustomFieldDefinitionDto>>(
            Arg.Is<CreateCustomFieldDefinition>(c =>
                c.EntityType == "Invoice" &&
                c.FieldKey == "priority" &&
                c.DisplayName == "Priority" &&
                c.FieldType == CustomFieldType.Dropdown &&
                c.Description == "Priority level" &&
                c.IsRequired),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_SetsRouteValuesWithId()
    {
        Guid definitionId = Guid.NewGuid();
        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "ref",
            DisplayName = "Ref",
            FieldType = CustomFieldType.Text
        };
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "ref", definitionId);
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(Arg.Any<CreateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues.Should().ContainKey("id");
        created.RouteValues!["id"].Should().Be(definitionId);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_WhenSuccess_ReturnsOkWithDefinition()
    {
        Guid definitionId = Guid.NewGuid();
        UpdateCustomFieldRequest request = new()
        {
            DisplayName = "Updated Name",
            Description = "Updated Desc"
        };
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "ref", definitionId);
        _bus.InvokeAsync<CustomFieldDefinitionDto>(Arg.Any<UpdateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        ActionResult<CustomFieldDefinitionDto> result = await _controller.Update(definitionId, request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CustomFieldDefinitionDto>();
    }

    [Fact]
    public async Task Update_PassesCorrectFieldsToCommand()
    {
        Guid definitionId = Guid.NewGuid();
        FieldValidationRules rules = new() { Min = 0, Max = 1000 };
        List<CustomFieldOption> options = new()
        {
            new CustomFieldOption { Value = "a", Label = "A" }
        };
        UpdateCustomFieldRequest request = new()
        {
            DisplayName = "Updated",
            Description = "Desc",
            IsRequired = true,
            DisplayOrder = 5,
            ValidationRules = rules,
            Options = options
        };
        CustomFieldDefinitionDto dto = CreateDefinitionDto("Invoice", "ref", definitionId);
        _bus.InvokeAsync<CustomFieldDefinitionDto>(Arg.Any<UpdateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        await _controller.Update(definitionId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<CustomFieldDefinitionDto>(
            Arg.Is<UpdateCustomFieldDefinition>(c =>
                c.Id == definitionId &&
                c.DisplayName == "Updated" &&
                c.Description == "Desc" &&
                c.IsRequired == true &&
                c.DisplayOrder == 5),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Deactivate

    [Fact]
    public async Task Deactivate_WhenSuccess_Returns204NoContent()
    {
        Guid definitionId = Guid.NewGuid();
        _bus.InvokeAsync(Arg.Any<DeactivateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        ActionResult result = await _controller.Deactivate(definitionId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Deactivate_PassesCorrectIdToCommand()
    {
        Guid definitionId = Guid.NewGuid();
        _bus.InvokeAsync(Arg.Any<DeactivateCustomFieldDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _controller.Deactivate(definitionId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<DeactivateCustomFieldDefinition>(c => c.Id == definitionId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Reorder

    [Fact]
    public async Task Reorder_WhenSuccess_Returns204NoContent()
    {
        List<Guid> fieldIds = new() { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ReorderFieldsRequest request = new() { FieldIds = fieldIds };
        _bus.InvokeAsync(Arg.Any<ReorderCustomFields>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        ActionResult result = await _controller.Reorder("Invoice", request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Reorder_PassesCorrectFieldsToCommand()
    {
        List<Guid> fieldIds = new() { Guid.NewGuid(), Guid.NewGuid() };
        ReorderFieldsRequest request = new() { FieldIds = fieldIds };
        _bus.InvokeAsync(Arg.Any<ReorderCustomFields>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _controller.Reorder("Payment", request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<ReorderCustomFields>(c =>
                c.EntityType == "Payment" &&
                c.FieldIdsInOrder == fieldIds),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helpers

    private CustomFieldDefinitionDto CreateDefinitionDto(string entityType, string fieldKey, Guid? id = null)
    {
        return new CustomFieldDefinitionDto
        {
            Id = id ?? Guid.NewGuid(),
            EntityType = entityType,
            FieldKey = fieldKey,
            DisplayName = fieldKey,
            FieldType = CustomFieldType.Text,
            DisplayOrder = 0,
            IsRequired = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    #endregion
}
