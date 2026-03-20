using Wallow.Billing.Api.Controllers;
using Wallow.Billing.Application.CustomFields.Commands.CreateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.Commands.ReorderCustomFields;
using Wallow.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitionById;
using Wallow.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitions;
using Wallow.Shared.Kernel.CustomFields;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Billing.Tests.Api.Controllers;

public class CustomFieldDefinitionsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly CustomFieldDefinitionsController _controller;

    public CustomFieldDefinitionsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new CustomFieldDefinitionsController(_bus);
    }

    #region GetByEntityType

    [Fact]
    public async Task GetByEntityType_ReturnsOkWithDefinitions()
    {
        List<CustomFieldDefinitionDto> dtos = [CreateDto(), CreateDto()];
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
                Arg.Any<GetCustomFieldDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(dtos);

        ActionResult<IReadOnlyList<CustomFieldDefinitionDto>> result =
            await _controller.GetByEntityType("Invoice", false, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<CustomFieldDefinitionDto> body = ok.Value.Should()
            .BeAssignableTo<IReadOnlyList<CustomFieldDefinitionDto>>().Subject;
        body.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByEntityType_PassesCorrectQueryToHandler()
    {
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
                Arg.Any<GetCustomFieldDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.GetByEntityType("Payment", true, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
            Arg.Is<GetCustomFieldDefinitionsQuery>(q =>
                q.EntityType == "Payment" && q.IncludeInactive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByEntityType_WithEmptyResult_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
                Arg.Any<GetCustomFieldDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ActionResult<IReadOnlyList<CustomFieldDefinitionDto>> result =
            await _controller.GetByEntityType("Invoice", false, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<CustomFieldDefinitionDto> body = ok.Value.Should()
            .BeAssignableTo<IReadOnlyList<CustomFieldDefinitionDto>>().Subject;
        body.Should().BeEmpty();
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithDto()
    {
        Guid id = Guid.NewGuid();
        CustomFieldDefinitionDto dto = CreateDto(id);
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(
                Arg.Any<GetCustomFieldDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        ActionResult<CustomFieldDefinitionDto> result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CustomFieldDefinitionDto body = ok.Value.Should().BeOfType<CustomFieldDefinitionDto>().Subject;
        body.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(
                Arg.Any<GetCustomFieldDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinitionDto?)null);

        ActionResult<CustomFieldDefinitionDto> result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<CustomFieldDefinitionDto?>(
                Arg.Any<GetCustomFieldDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((CustomFieldDefinitionDto?)null);

        await _controller.GetById(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<CustomFieldDefinitionDto?>(
            Arg.Is<GetCustomFieldDefinitionByIdQuery>(q => q.Id == id),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        CustomFieldDefinitionDto dto = CreateDto();
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
                Arg.Any<CreateCustomFieldDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "po_number",
            DisplayName = "PO Number",
            FieldType = CustomFieldType.Text
        };

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task Create_WhenConflict_ReturnsBadRequest()
    {
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
                Arg.Any<CreateCustomFieldDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CustomFieldDefinitionDto>(Error.Conflict("Field key already exists")));

        CreateCustomFieldRequest request = new()
        {
            EntityType = "Invoice",
            FieldKey = "existing_field",
            DisplayName = "Existing",
            FieldType = CustomFieldType.Text
        };

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Create_PassesCorrectFieldsToCommand()
    {
        CustomFieldDefinitionDto dto = CreateDto();
        _bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
                Arg.Any<CreateCustomFieldDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        CreateCustomFieldRequest request = new()
        {
            EntityType = "Payment",
            FieldKey = "ref_code",
            DisplayName = "Reference Code",
            FieldType = CustomFieldType.Text,
            IsRequired = true
        };

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<CustomFieldDefinitionDto>>(
            Arg.Is<CreateCustomFieldDefinitionCommand>(c =>
                c.EntityType == "Payment" &&
                c.FieldKey == "ref_code" &&
                c.DisplayName == "Reference Code" &&
                c.FieldType == CustomFieldType.Text &&
                c.IsRequired),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOkWithDto()
    {
        Guid id = Guid.NewGuid();
        CustomFieldDefinitionDto dto = CreateDto(id);
        _bus.InvokeAsync<CustomFieldDefinitionDto>(
                Arg.Any<UpdateCustomFieldDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        UpdateCustomFieldRequest request = new() { DisplayName = "Updated Name" };

        ActionResult<CustomFieldDefinitionDto> result = await _controller.Update(id, request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CustomFieldDefinitionDto body = ok.Value.Should().BeOfType<CustomFieldDefinitionDto>().Subject;
        body.Id.Should().Be(id);
    }

    [Fact]
    public async Task Update_PassesCorrectIdAndFieldsToCommand()
    {
        Guid id = Guid.NewGuid();
        CustomFieldDefinitionDto dto = CreateDto(id);
        _bus.InvokeAsync<CustomFieldDefinitionDto>(
                Arg.Any<UpdateCustomFieldDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        UpdateCustomFieldRequest request = new()
        {
            DisplayName = "New Name",
            IsRequired = true,
            DisplayOrder = 2
        };

        await _controller.Update(id, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<CustomFieldDefinitionDto>(
            Arg.Is<UpdateCustomFieldDefinitionCommand>(c =>
                c.Id == id &&
                c.DisplayName == "New Name" &&
                c.IsRequired == true &&
                c.DisplayOrder == 2),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Deactivate

    [Fact]
    public async Task Deactivate_WhenSuccessful_Returns204NoContent()
    {
        Guid id = Guid.NewGuid();

        ActionResult result = await _controller.Deactivate(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Deactivate_PassesCorrectIdToCommand()
    {
        Guid id = Guid.NewGuid();

        await _controller.Deactivate(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<DeactivateCustomFieldDefinitionCommand>(c => c.Id == id),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Reorder

    [Fact]
    public async Task Reorder_WhenSuccessful_Returns204NoContent()
    {
        ReorderFieldsRequest request = new() { FieldIds = [Guid.NewGuid(), Guid.NewGuid()] };

        ActionResult result = await _controller.Reorder("Invoice", request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Reorder_PassesCorrectEntityTypeAndIdsToCommand()
    {
        IReadOnlyList<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        ReorderFieldsRequest request = new() { FieldIds = ids };

        await _controller.Reorder("Payment", request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<ReorderCustomFieldsCommand>(c =>
                c.EntityType == "Payment" &&
                c.FieldIdsInOrder.Count == 2),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helpers

    private static CustomFieldDefinitionDto CreateDto(Guid? id = null)
    {
        return new CustomFieldDefinitionDto
        {
            Id = id ?? Guid.NewGuid(),
            EntityType = "Invoice",
            FieldKey = "test_field",
            DisplayName = "Test Field",
            FieldType = CustomFieldType.Text,
            DisplayOrder = 0,
            IsRequired = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };
    }

    #endregion
}
