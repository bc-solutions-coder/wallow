using Foundry.Billing.Api.Controllers;
using Foundry.Billing.Application.Metering.Commands.RemoveQuotaOverride;
using Foundry.Billing.Application.Metering.Commands.SetQuotaOverride;
using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Queries.GetQuotaStatus;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Tests.Api.Controllers;

public class QuotasControllerTests
{
    private readonly IMessageBus _bus;
    private readonly QuotasController _controller;

    public QuotasControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new QuotasController(_bus)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetAll

    [Fact]
    public async Task GetAll_WhenSuccess_ReturnsOkWithQuotaStatuses()
    {
        List<QuotaStatusDto> quotas = new()
        {
            new QuotaStatusDto("api-calls", "API Calls", 500, 1000, 50.0m, "Monthly", "Block", false),
            new QuotaStatusDto("storage", "Storage", 800, 1000, 80.0m, "Monthly", "Warn", true)
        };
        _bus.InvokeAsync<Result<IReadOnlyList<QuotaStatusDto>>>(Arg.Any<GetQuotaStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<QuotaStatusDto>>(quotas));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<QuotaStatusDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<QuotaStatusDto>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<QuotaStatusDto>>>(Arg.Any<GetQuotaStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<QuotaStatusDto>>([]));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<QuotaStatusDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<QuotaStatusDto>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<QuotaStatusDto>>>(Arg.Any<GetQuotaStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<QuotaStatusDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region SetOverride

    [Fact]
    public async Task SetOverride_WhenSuccess_ReturnsOk()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideRequest request = new("api-calls", 5000, QuotaPeriod.Monthly, QuotaAction.Block);
        _bus.InvokeAsync<Result>(Arg.Any<SetQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.SetOverride(tenantId, request, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task SetOverride_PassesCorrectFieldsToCommand()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideRequest request = new("storage", 10000, QuotaPeriod.Daily, QuotaAction.Warn);
        _bus.InvokeAsync<Result>(Arg.Any<SetQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.SetOverride(tenantId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<SetQuotaOverrideCommand>(c =>
                c.TenantId == tenantId &&
                c.MeterCode == "storage" &&
                c.Limit == 10000 &&
                c.Period == QuotaPeriod.Daily &&
                c.OnExceeded == QuotaAction.Warn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverride_WhenNotFound_Returns404()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideRequest request = new("api-calls", 5000, QuotaPeriod.Monthly, QuotaAction.Block);
        _bus.InvokeAsync<Result>(Arg.Any<SetQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Tenant", tenantId)));

        IActionResult result = await _controller.SetOverride(tenantId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SetOverride_WhenValidationFailure_Returns400()
    {
        Guid tenantId = Guid.NewGuid();
        SetQuotaOverrideRequest request = new("", -1, QuotaPeriod.Monthly, QuotaAction.Block);
        _bus.InvokeAsync<Result>(Arg.Any<SetQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Meter code is required")));

        IActionResult result = await _controller.SetOverride(tenantId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region RemoveOverride

    [Fact]
    public async Task RemoveOverride_WhenSuccess_Returns204NoContent()
    {
        Guid tenantId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<RemoveQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.RemoveOverride(tenantId, "api-calls", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveOverride_PassesCorrectFieldsToCommand()
    {
        Guid tenantId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<RemoveQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.RemoveOverride(tenantId, "storage", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<RemoveQuotaOverrideCommand>(c => c.TenantId == tenantId && c.MeterCode == "storage"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOverride_WhenNotFound_Returns404()
    {
        Guid tenantId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<RemoveQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("QuotaOverride", "api-calls")));

        IActionResult result = await _controller.RemoveOverride(tenantId, "api-calls", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RemoveOverride_WhenValidationFailure_Returns400()
    {
        Guid tenantId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<RemoveQuotaOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Cannot remove default quota")));

        IActionResult result = await _controller.RemoveOverride(tenantId, "api-calls", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion
}
