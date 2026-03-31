using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Api.Contracts.Payments;
using Wallow.Billing.Api.Controllers;
using Wallow.Billing.Application.Commands.ProcessPayment;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Queries.GetAllPayments;
using Wallow.Billing.Application.Queries.GetPaymentById;
using Wallow.Billing.Application.Queries.GetPaymentsByInvoiceId;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Billing.Tests.Api.Controllers;

public class PaymentsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly PaymentsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);
        _controller = new PaymentsController(_bus, _currentUserService);

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
    public async Task GetAll_WhenSuccess_ReturnsOkWithPaymentResponses()
    {
        List<PaymentDto> payments = [CreatePaymentDto(), CreatePaymentDto()];
        PagedResult<PaymentDto> paged = new(payments, 2, 1, 50);
        _bus.InvokeAsync<Result<PagedResult<PaymentDto>>>(Arg.Any<GetAllPaymentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(paged));

        IActionResult result = await _controller.GetAll(cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PagedResult<PaymentResponse> responses = ok.Value.Should().BeOfType<PagedResult<PaymentResponse>>().Subject;
        responses.Items.Should().HaveCount(2);
        responses.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        PagedResult<PaymentDto> paged = new([], 0, 1, 50);
        _bus.InvokeAsync<Result<PagedResult<PaymentDto>>>(Arg.Any<GetAllPaymentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(paged));

        IActionResult result = await _controller.GetAll(cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PagedResult<PaymentResponse> responses = ok.Value.Should().BeOfType<PagedResult<PaymentResponse>>().Subject;
        responses.Items.Should().BeEmpty();
        responses.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<PagedResult<PaymentDto>>>(Arg.Any<GetAllPaymentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PagedResult<PaymentDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetAll_ForwardsSkipAndTakeToQuery()
    {
        PagedResult<PaymentDto> paged = new([], 0, 1, 25);
        _bus.InvokeAsync<Result<PagedResult<PaymentDto>>>(Arg.Any<GetAllPaymentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(paged));

        await _controller.GetAll(skip: 10, take: 25, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PagedResult<PaymentDto>>>(
            Arg.Is<GetAllPaymentsQuery>(q => q.Skip == 10 && q.Take == 25),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithPaymentResponse()
    {
        Guid paymentId = Guid.NewGuid();
        PaymentDto dto = CreatePaymentDto(paymentId);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<GetPaymentByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(paymentId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PaymentResponse response = ok.Value.Should().BeOfType<PaymentResponse>().Subject;
        response.Id.Should().Be(paymentId);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid paymentId = Guid.NewGuid();
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<GetPaymentByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentDto>(Error.NotFound("Payment", paymentId)));

        IActionResult result = await _controller.GetById(paymentId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid paymentId = Guid.NewGuid();
        PaymentDto dto = CreatePaymentDto(paymentId);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<GetPaymentByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetById(paymentId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PaymentDto>>(
            Arg.Is<GetPaymentByIdQuery>(q => q.PaymentId == paymentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_MapsAllDtoFieldsToResponse()
    {
        Guid paymentId = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        DateTime completedAt = DateTime.UtcNow;
        DateTime createdAt = DateTime.UtcNow.AddHours(-1);
        DateTime updatedAt = DateTime.UtcNow;
        PaymentDto dto = new(paymentId, invoiceId, _userId, 250.00m, "USD", "CreditCard",
            "Completed", "TXN-123", null, completedAt, createdAt, updatedAt, null);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<GetPaymentByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(paymentId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PaymentResponse response = ok.Value.Should().BeOfType<PaymentResponse>().Subject;
        response.Id.Should().Be(paymentId);
        response.InvoiceId.Should().Be(invoiceId);
        response.UserId.Should().Be(_userId);
        response.Amount.Should().Be(250.00m);
        response.Currency.Should().Be("USD");
        response.Method.Should().Be("CreditCard");
        response.Status.Should().Be("Completed");
        response.TransactionReference.Should().Be("TXN-123");
        response.FailureReason.Should().BeNull();
        response.CompletedAt.Should().Be(completedAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region GetByInvoiceId

    [Fact]
    public async Task GetByInvoiceId_WhenSuccess_ReturnsOkWithPayments()
    {
        Guid invoiceId = Guid.NewGuid();
        List<PaymentDto> payments = new()
        {
            CreatePaymentDto(invoiceId: invoiceId),
            CreatePaymentDto(invoiceId: invoiceId)
        };
        _bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(Arg.Any<GetPaymentsByInvoiceIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<PaymentDto>>(payments));

        IActionResult result = await _controller.GetByInvoiceId(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<PaymentResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<PaymentResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByInvoiceId_WhenEmpty_ReturnsOkWithEmptyList()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(Arg.Any<GetPaymentsByInvoiceIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<PaymentDto>>([]));

        IActionResult result = await _controller.GetByInvoiceId(invoiceId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<PaymentResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<PaymentResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByInvoiceId_PassesCorrectInvoiceIdToQuery()
    {
        Guid invoiceId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(Arg.Any<GetPaymentsByInvoiceIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<PaymentDto>>([]));

        await _controller.GetByInvoiceId(invoiceId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(
            Arg.Is<GetPaymentsByInvoiceIdQuery>(q => q.InvoiceId == invoiceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByInvoiceId_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(Arg.Any<GetPaymentsByInvoiceIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<PaymentDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetByInvoiceId(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region ProcessPayment

    [Fact]
    public async Task ProcessPayment_WithValidRequest_Returns201Created()
    {
        Guid invoiceId = Guid.NewGuid();
        ProcessPaymentRequest request = new(100.00m, "USD", "CreditCard");
        PaymentDto dto = CreatePaymentDto(invoiceId: invoiceId);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeOfType<PaymentResponse>();
    }

    [Fact]
    public async Task ProcessPayment_PassesCorrectFieldsToCommand()
    {
        Guid invoiceId = Guid.NewGuid();
        ProcessPaymentRequest request = new(250.50m, "EUR", "BankTransfer");
        PaymentDto dto = CreatePaymentDto(invoiceId: invoiceId);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PaymentDto>>(
            Arg.Is<ProcessPaymentCommand>(c =>
                c.InvoiceId == invoiceId &&
                c.UserId == _userId &&
                c.Amount == 250.50m &&
                c.Currency == "EUR" &&
                c.PaymentMethod == "BankTransfer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPayment_SetsLocationHeader()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid paymentId = Guid.NewGuid();
        ProcessPaymentRequest request = new(100.00m, "USD", "CreditCard");
        PaymentDto dto = CreatePaymentDto(paymentId, invoiceId);
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/billing/payments/{paymentId}");
    }

    [Fact]
    public async Task ProcessPayment_WhenNotFound_ReturnsErrorResult()
    {
        Guid invoiceId = Guid.NewGuid();
        ProcessPaymentRequest request = new(100.00m, "USD", "CreditCard");
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentDto>(Error.NotFound("Invoice", invoiceId)));

        IActionResult result = await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ProcessPayment_WhenValidationFailure_ReturnsErrorResult()
    {
        Guid invoiceId = Guid.NewGuid();
        ProcessPaymentRequest request = new(0m, "USD", "CreditCard");
        _bus.InvokeAsync<Result<PaymentDto>>(Arg.Any<ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentDto>(Error.Validation("Amount must be positive")));

        IActionResult result = await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ProcessPayment_WithNoUserClaims_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        Guid invoiceId = Guid.NewGuid();
        ProcessPaymentRequest request = new(100.00m, "USD", "CreditCard");

        IActionResult result = await _controller.ProcessPayment(invoiceId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region Helpers

    private PaymentDto CreatePaymentDto(Guid? id = null, Guid? invoiceId = null)
    {
        return new PaymentDto(
            id ?? Guid.NewGuid(),
            invoiceId ?? Guid.NewGuid(),
            _userId,
            100.00m,
            "USD",
            "CreditCard",
            "Pending",
            null,
            null,
            null,
            DateTime.UtcNow,
            null,
            null);
    }

    #endregion
}
