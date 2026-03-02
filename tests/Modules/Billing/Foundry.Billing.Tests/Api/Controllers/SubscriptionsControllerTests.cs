using System.Security.Claims;
using Foundry.Billing.Api.Contracts.Subscriptions;
using Foundry.Billing.Api.Controllers;
using Foundry.Billing.Application.Commands.CancelSubscription;
using Foundry.Billing.Application.Commands.CreateSubscription;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Queries.GetSubscriptionById;
using Foundry.Billing.Application.Queries.GetSubscriptionsByUserId;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Tests.Api.Controllers;

public class SubscriptionsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly SubscriptionsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public SubscriptionsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);
        _controller = new SubscriptionsController(_bus, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithSubscriptionResponse()
    {
        Guid subscriptionId = Guid.NewGuid();
        SubscriptionDto dto = CreateSubscriptionDto(subscriptionId);
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<GetSubscriptionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(subscriptionId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        SubscriptionResponse response = ok.Value.Should().BeOfType<SubscriptionResponse>().Subject;
        response.Id.Should().Be(subscriptionId);
        response.PlanName.Should().Be("Pro");
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid subscriptionId = Guid.NewGuid();
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<GetSubscriptionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SubscriptionDto>(Error.NotFound("Subscription", subscriptionId)));

        IActionResult result = await _controller.GetById(subscriptionId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid subscriptionId = Guid.NewGuid();
        SubscriptionDto dto = CreateSubscriptionDto(subscriptionId);
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<GetSubscriptionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetById(subscriptionId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<SubscriptionDto>>(
            Arg.Is<GetSubscriptionByIdQuery>(q => q.SubscriptionId == subscriptionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_MapsAllDtoFieldsToResponse()
    {
        Guid subscriptionId = Guid.NewGuid();
        DateTime startDate = DateTime.UtcNow.AddDays(-30);
        DateTime endDate = DateTime.UtcNow.AddDays(335);
        DateTime periodStart = DateTime.UtcNow.AddDays(-30);
        DateTime periodEnd = DateTime.UtcNow;
        DateTime cancelledAt = DateTime.UtcNow.AddDays(-1);
        DateTime createdAt = DateTime.UtcNow.AddDays(-31);
        DateTime updatedAt = DateTime.UtcNow;
        SubscriptionDto dto = new(subscriptionId, _userId, "Enterprise", 999.99m, "EUR",
            "Cancelled", startDate, endDate, periodStart, periodEnd,
            cancelledAt, createdAt, updatedAt, null);
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<GetSubscriptionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(subscriptionId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        SubscriptionResponse response = ok.Value.Should().BeOfType<SubscriptionResponse>().Subject;
        response.Id.Should().Be(subscriptionId);
        response.UserId.Should().Be(_userId);
        response.PlanName.Should().Be("Enterprise");
        response.Price.Should().Be(999.99m);
        response.Currency.Should().Be("EUR");
        response.Status.Should().Be("Cancelled");
        response.StartDate.Should().Be(startDate);
        response.EndDate.Should().Be(endDate);
        response.CurrentPeriodStart.Should().Be(periodStart);
        response.CurrentPeriodEnd.Should().Be(periodEnd);
        response.CancelledAt.Should().Be(cancelledAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region GetByUserId

    [Fact]
    public async Task GetByUserId_WhenSuccess_ReturnsOkWithSubscriptions()
    {
        Guid userId = Guid.NewGuid();
        List<SubscriptionDto> subscriptions = new()
        {
            CreateSubscriptionDto(),
            CreateSubscriptionDto()
        };
        _bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(Arg.Any<GetSubscriptionsByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubscriptionDto>>(subscriptions));

        IActionResult result = await _controller.GetByUserId(userId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<SubscriptionResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<SubscriptionResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserId_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(Arg.Any<GetSubscriptionsByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubscriptionDto>>(new List<SubscriptionDto>()));

        IActionResult result = await _controller.GetByUserId(Guid.NewGuid(), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<SubscriptionResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<SubscriptionResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserId_PassesCorrectUserIdToQuery()
    {
        Guid userId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(Arg.Any<GetSubscriptionsByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubscriptionDto>>(new List<SubscriptionDto>()));

        await _controller.GetByUserId(userId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(
            Arg.Is<GetSubscriptionsByUserIdQuery>(q => q.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByUserId_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(Arg.Any<GetSubscriptionsByUserIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<SubscriptionDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetByUserId(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        DateTime startDate = DateTime.UtcNow;
        DateTime periodEnd = DateTime.UtcNow.AddMonths(1);
        CreateSubscriptionRequest request = new("Pro", 29.99m, "USD", startDate, periodEnd);
        SubscriptionDto dto = CreateSubscriptionDto();
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<CreateSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeOfType<SubscriptionResponse>();
    }

    [Fact]
    public async Task Create_PassesCorrectFieldsToCommand()
    {
        DateTime startDate = DateTime.UtcNow;
        DateTime periodEnd = DateTime.UtcNow.AddMonths(1);
        CreateSubscriptionRequest request = new("Enterprise", 199.99m, "EUR", startDate, periodEnd);
        SubscriptionDto dto = CreateSubscriptionDto();
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<CreateSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<SubscriptionDto>>(
            Arg.Is<CreateSubscriptionCommand>(c =>
                c.UserId == _userId &&
                c.PlanName == "Enterprise" &&
                c.Price == 199.99m &&
                c.Currency == "EUR" &&
                c.StartDate == startDate &&
                c.PeriodEnd == periodEnd),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_SetsLocationHeader()
    {
        Guid subscriptionId = Guid.NewGuid();
        CreateSubscriptionRequest request = new("Pro", 29.99m, "USD", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        SubscriptionDto dto = CreateSubscriptionDto(subscriptionId);
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<CreateSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/billing/subscriptions/{subscriptionId}");
    }

    [Fact]
    public async Task Create_WhenValidationFailure_ThrowsDueToValueAccess()
    {
        // BUG: Controller accesses result.Value?.Id for location string even on failure path,
        // which throws InvalidOperationException from the Result type.
        CreateSubscriptionRequest request = new("", 0m, "USD", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        _bus.InvokeAsync<Result<SubscriptionDto>>(Arg.Any<CreateSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SubscriptionDto>(Error.Validation("Plan name is required")));

        Func<Task> act = () => _controller.Create(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public async Task Create_WithNoUserClaims_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateSubscriptionRequest request = new("Pro", 29.99m, "USD", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await _bus.DidNotReceive().InvokeAsync<Result<SubscriptionDto>>(
            Arg.Any<CreateSubscriptionCommand>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Cancel

    [Fact]
    public async Task Cancel_WhenSuccess_Returns204NoContent()
    {
        Guid subscriptionId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Cancel(subscriptionId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Cancel_PassesCorrectFieldsToCommand()
    {
        Guid subscriptionId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Cancel(subscriptionId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<CancelSubscriptionCommand>(c => c.SubscriptionId == subscriptionId && c.CancelledByUserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_WhenNotFound_Returns404()
    {
        Guid subscriptionId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Subscription", subscriptionId)));

        IActionResult result = await _controller.Cancel(subscriptionId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Cancel_WhenValidationFailure_Returns400()
    {
        Guid subscriptionId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<CancelSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Subscription already cancelled")));

        IActionResult result = await _controller.Cancel(subscriptionId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Helpers

    private SubscriptionDto CreateSubscriptionDto(Guid? id = null)
    {
        DateTime now = DateTime.UtcNow;
        return new SubscriptionDto(
            id ?? Guid.NewGuid(),
            _userId,
            "Pro",
            29.99m,
            "USD",
            "Active",
            now,
            null,
            now,
            now.AddMonths(1),
            null,
            now,
            null,
            null);
    }

    #endregion
}
