using Foundry.Billing.Api.Controllers;
using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Queries.GetCurrentUsage;
using Foundry.Billing.Application.Metering.Queries.GetUsageHistory;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Tests.Api.Controllers;

public class UsageControllerTests
{
    private readonly IMessageBus _bus;
    private readonly UsageController _controller;

    public UsageControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new UsageController(_bus)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetAll

    [Fact]
    public async Task GetAll_WhenSuccess_ReturnsOkWithUsageSummaries()
    {
        List<UsageSummaryDto> usage = new()
        {
            new UsageSummaryDto("api-calls", "API Calls", "requests", 500, 1000, 50.0m, "Monthly"),
            new UsageSummaryDto("storage", "Storage", "bytes", 800, null, null, "Monthly")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageSummaryDto>>(usage));

        IActionResult result = await _controller.GetAll(null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UsageSummaryDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<UsageSummaryDto>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_WithPeriod_PassesPeriodToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageSummaryDto>>([]));

        await _controller.GetAll(QuotaPeriod.Daily, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(
            Arg.Is<GetCurrentUsageQuery>(q => q.Period == QuotaPeriod.Daily && q.MeterCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WithoutPeriod_PassesNullPeriodToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageSummaryDto>>([]));

        await _controller.GetAll(null, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(
            Arg.Is<GetCurrentUsageQuery>(q => q.Period == null && q.MeterCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<UsageSummaryDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(null, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region GetByMeterCode

    [Fact]
    public async Task GetByMeterCode_WhenSuccess_ReturnsOkWithUsageSummaries()
    {
        List<UsageSummaryDto> usage = new()
        {
            new UsageSummaryDto("api-calls", "API Calls", "requests", 500, 1000, 50.0m, "Monthly")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageSummaryDto>>(usage));

        IActionResult result = await _controller.GetByMeterCode("api-calls", null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UsageSummaryDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<UsageSummaryDto>>().Subject;
        responses.Should().HaveCount(1);
        responses[0].MeterCode.Should().Be("api-calls");
    }

    [Fact]
    public async Task GetByMeterCode_PassesCorrectMeterCodeToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageSummaryDto>>([]));

        await _controller.GetByMeterCode("storage", QuotaPeriod.Monthly, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(
            Arg.Is<GetCurrentUsageQuery>(q => q.MeterCode == "storage" && q.Period == QuotaPeriod.Monthly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByMeterCode_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageSummaryDto>>>(Arg.Any<GetCurrentUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<UsageSummaryDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetByMeterCode("api-calls", null, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region GetHistory

    [Fact]
    public async Task GetHistory_WhenSuccess_ReturnsOkWithUsageRecords()
    {
        DateTime from = DateTime.UtcNow.AddDays(-7);
        DateTime to = DateTime.UtcNow;
        List<UsageRecordDto> records = new()
        {
            new UsageRecordDto(Guid.NewGuid(), Guid.NewGuid(), "api-calls", from, from.AddDays(1), 100, from.AddDays(1)),
            new UsageRecordDto(Guid.NewGuid(), Guid.NewGuid(), "api-calls", from.AddDays(1), from.AddDays(2), 150, from.AddDays(2))
        };
        _bus.InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(Arg.Any<GetUsageHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageRecordDto>>(records));

        IActionResult result = await _controller.GetHistory("api-calls", from, to, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UsageRecordDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<UsageRecordDto>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistory_PassesCorrectFieldsToQuery()
    {
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow;
        _bus.InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(Arg.Any<GetUsageHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageRecordDto>>([]));

        await _controller.GetHistory("storage", from, to, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(
            Arg.Is<GetUsageHistoryQuery>(q => q.MeterCode == "storage" && q.From == from && q.To == to),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHistory_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(Arg.Any<GetUsageHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<UsageRecordDto>>([]));

        IActionResult result = await _controller.GetHistory("api-calls", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UsageRecordDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<UsageRecordDto>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<UsageRecordDto>>>(Arg.Any<GetUsageHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<UsageRecordDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetHistory("api-calls", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion
}
