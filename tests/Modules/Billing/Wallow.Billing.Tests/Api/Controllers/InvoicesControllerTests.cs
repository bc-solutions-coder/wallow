using System.Security.Claims;
using Wallow.Billing.Api.Contracts.Invoices;
using Wallow.Billing.Api.Controllers;
using Wallow.Billing.Application.Commands.AddLineItem;
using Wallow.Billing.Application.Commands.CancelInvoice;
using Wallow.Billing.Application.Commands.CreateInvoice;
using Wallow.Billing.Application.Commands.IssueInvoice;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Queries.GetAllInvoices;
using Wallow.Billing.Application.Queries.GetInvoiceById;
using Wallow.Billing.Application.Queries.GetInvoicesByUserId;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Billing.Tests.Api.Controllers;

public class InvoicesControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly InvoicesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public InvoicesControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);
        _controller = new InvoicesController(_bus, _currentUserService);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetAll

    [Fact]
    public async Task GetAll_WhenSuccess_ReturnsOkWithInvoiceResponses()
    {
        List<InvoiceDto> invoices = new()
        {
            CreateInvoiceDto("INV-001"),
            CreateInvoiceDto("INV-002")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetAllInvoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InvoiceDto>>(invoices));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InvoiceResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InvoiceResponse>>().Subject;
        responses.Should().HaveCount(2);
        responses[0].InvoiceNumber.Should().Be("INV-001");
        responses[1].InvoiceNumber.Should().Be("INV-002");
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetAllInvoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InvoiceDto>>([]));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InvoiceResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InvoiceResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetAllInvoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<InvoiceDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithInvoiceResponse()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InvoiceResponse response = ok.Value.Should().BeOfType<InvoiceResponse>().Subject;
        response.Id.Should().Be(invoiceId);
        response.InvoiceNumber.Should().Be("INV-001");
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.NotFound("Invoice", invoiceId)));

        IActionResult result = await _controller.GetById(invoiceId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetById(invoiceId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<GetInvoiceByIdQuery>(q => q.InvoiceId == invoiceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_MapsLineItemsCorrectly()
    {
        Guid invoiceId = Guid.NewGuid();
        List<InvoiceLineItemDto> lineItems = new()
        {
            new InvoiceLineItemDto(Guid.NewGuid(), "Service A", 100.00m, "USD", 2, 200.00m),
            new InvoiceLineItemDto(Guid.NewGuid(), "Service B", 50.00m, "USD", 1, 50.00m)
        };
        InvoiceDto dto = new(invoiceId, _userId, "INV-001", "Draft", 250.00m, "USD",
            null, null, DateTime.UtcNow, null, lineItems, null);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InvoiceResponse response = ok.Value.Should().BeOfType<InvoiceResponse>().Subject;
        response.LineItems.Should().HaveCount(2);
        response.LineItems[0].Description.Should().Be("Service A");
        response.LineItems[0].UnitPrice.Should().Be(100.00m);
        response.LineItems[0].Quantity.Should().Be(2);
        response.LineItems[0].LineTotal.Should().Be(200.00m);
        response.LineItems[1].Description.Should().Be("Service B");
    }

    #endregion

    #region GetByUserId

    [Fact]
    public async Task GetByUserId_WhenSuccess_ReturnsOkWithInvoices()
    {
        Guid userId = Guid.NewGuid();
        List<InvoiceDto> invoices = new() { CreateInvoiceDto("INV-001") };
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetInvoicesByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InvoiceDto>>(invoices));

        IActionResult result = await _controller.GetByUserId(userId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InvoiceResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InvoiceResponse>>().Subject;
        responses.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByUserId_PassesCorrectUserIdToQuery()
    {
        Guid userId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetInvoicesByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InvoiceDto>>([]));

        await _controller.GetByUserId(userId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(
            Arg.Is<GetInvoicesByUserIdQuery>(q => q.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByUserId_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(Arg.Any<GetInvoicesByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<InvoiceDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetByUserId(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        CreateInvoiceRequest request = new("INV-001", "USD", DateTime.UtcNow.AddDays(30));
        InvoiceDto dto = CreateInvoiceDto("INV-001");
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        InvoiceResponse response = created.Value.Should().BeOfType<InvoiceResponse>().Subject;
        response.InvoiceNumber.Should().Be("INV-001");
        response.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Create_PassesUserIdFromClaims()
    {
        CreateInvoiceRequest request = new("INV-001", "USD", null);
        InvoiceDto dto = CreateInvoiceDto("INV-001");
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<CreateInvoiceCommand>(c => c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PassesRequestFieldsToCommand()
    {
        DateTime dueDate = DateTime.UtcNow.AddDays(30);
        CreateInvoiceRequest request = new("INV-001", "EUR", dueDate);
        InvoiceDto dto = CreateInvoiceDto("INV-001");
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<CreateInvoiceCommand>(c =>
                c.InvoiceNumber == "INV-001" &&
                c.Currency == "EUR" &&
                c.DueDate == dueDate),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenFailure_ReturnsErrorResult()
    {
        CreateInvoiceRequest request = new("INV-001", "USD", null);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.Validation("Invalid invoice")));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_SetsLocationHeader()
    {
        Guid invoiceId = Guid.NewGuid();
        CreateInvoiceRequest request = new("INV-001", "USD", null);
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/billing/invoices/{invoiceId}");
    }

    #endregion

    #region AddLineItem

    [Fact]
    public async Task AddLineItem_WithValidRequest_ReturnsOkWithInvoice()
    {
        Guid invoiceId = Guid.NewGuid();
        AddLineItemRequest request = new("Consulting", 150.00m, 3);
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<AddLineItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.AddLineItem(invoiceId, request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<InvoiceResponse>();
    }

    [Fact]
    public async Task AddLineItem_PassesCorrectFieldsToCommand()
    {
        Guid invoiceId = Guid.NewGuid();
        AddLineItemRequest request = new("Development", 200.00m, 5);
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<AddLineItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.AddLineItem(invoiceId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<AddLineItemCommand>(c =>
                c.InvoiceId == invoiceId &&
                c.Description == "Development" &&
                c.UnitPrice == 200.00m &&
                c.Quantity == 5 &&
                c.UpdatedByUserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddLineItem_WhenNotFound_Returns404()
    {
        Guid invoiceId = Guid.NewGuid();
        AddLineItemRequest request = new("Service", 100.00m, 1);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<AddLineItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.NotFound("Invoice", invoiceId)));

        IActionResult result = await _controller.AddLineItem(invoiceId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AddLineItem_WhenValidationFailure_Returns400()
    {
        Guid invoiceId = Guid.NewGuid();
        AddLineItemRequest request = new("Service", -10.00m, 1);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<AddLineItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.Validation("Invalid line item")));

        IActionResult result = await _controller.AddLineItem(invoiceId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Issue

    [Fact]
    public async Task Issue_WhenSuccess_ReturnsOkWithInvoice()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<IssueInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Issue(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<InvoiceResponse>();
    }

    [Fact]
    public async Task Issue_PassesCorrectFieldsToCommand()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceDto dto = CreateInvoiceDto("INV-001", invoiceId);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<IssueInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Issue(invoiceId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<IssueInvoiceCommand>(c => c.InvoiceId == invoiceId && c.IssuedByUserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issue_WhenNotFound_Returns404()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<IssueInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.NotFound("Invoice", invoiceId)));

        IActionResult result = await _controller.Issue(invoiceId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Issue_WhenValidationFailure_Returns400()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<IssueInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InvoiceDto>(Error.Validation("Cannot issue invoice without line items")));

        IActionResult result = await _controller.Issue(invoiceId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Cancel

    [Fact]
    public async Task Cancel_WhenSuccess_Returns204NoContent()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Cancel(invoiceId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Cancel_PassesCorrectFieldsToCommand()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Cancel(invoiceId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<CancelInvoiceCommand>(c => c.InvoiceId == invoiceId && c.CancelledByUserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_WhenNotFound_Returns404()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Invoice", invoiceId)));

        IActionResult result = await _controller.Cancel(invoiceId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Cancel_WhenValidationFailure_Returns400()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Cannot cancel a paid invoice")));

        IActionResult result = await _controller.Cancel(invoiceId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region User Claims

    [Fact]
    public async Task Create_WithNoUserClaims_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateInvoiceRequest request = new("INV-001", "USD", null);

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Create_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        CreateInvoiceRequest request = new("INV-001", "USD", null);
        InvoiceDto dto = CreateInvoiceDto("INV-001");
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InvoiceDto>>(
            Arg.Is<CreateInvoiceCommand>(c => c.UserId == subUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithNonGuidUserClaim_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateInvoiceRequest request = new("INV-001", "USD", null);

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region Response Mapping

    [Fact]
    public async Task GetById_MapsAllDtoFieldsToResponse()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime dueDate = DateTime.UtcNow.AddDays(30);
        DateTime paidAt = DateTime.UtcNow;
        DateTime createdAt = DateTime.UtcNow.AddDays(-1);
        DateTime updatedAt = DateTime.UtcNow;
        InvoiceDto dto = new(invoiceId, userId, "INV-999", "Paid", 500.00m, "EUR",
            dueDate, paidAt, createdAt, updatedAt, [], null);
        _bus.InvokeAsync<Result<InvoiceDto>>(Arg.Any<GetInvoiceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InvoiceResponse response = ok.Value.Should().BeOfType<InvoiceResponse>().Subject;
        response.Id.Should().Be(invoiceId);
        response.UserId.Should().Be(userId);
        response.InvoiceNumber.Should().Be("INV-999");
        response.Status.Should().Be("Paid");
        response.TotalAmount.Should().Be(500.00m);
        response.Currency.Should().Be("EUR");
        response.DueDate.Should().Be(dueDate);
        response.PaidAt.Should().Be(paidAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
        response.LineItems.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private InvoiceDto CreateInvoiceDto(string invoiceNumber, Guid? id = null)
    {
        return new InvoiceDto(
            id ?? Guid.NewGuid(),
            _userId,
            invoiceNumber,
            "Draft",
            0m,
            "USD",
            null,
            null,
            DateTime.UtcNow,
            null,
            [],
            null);
    }

    #endregion
}
