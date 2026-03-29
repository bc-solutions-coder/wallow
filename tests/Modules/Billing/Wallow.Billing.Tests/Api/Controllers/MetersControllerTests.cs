using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Api.Controllers;
using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Queries.GetMeterDefinitions;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Billing.Tests.Api.Controllers;

public class MetersControllerTests
{
    private readonly IMessageBus _bus;
    private readonly MetersController _controller;

    public MetersControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new MetersController(_bus)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GetAll_WhenSuccess_ReturnsOkWithMeterDefinitions()
    {
        List<MeterDefinitionDto> meters = new()
        {
            new MeterDefinitionDto(Guid.NewGuid(), "api-calls", "API Calls", "requests", "Sum", true),
            new MeterDefinitionDto(Guid.NewGuid(), "storage", "Storage", "bytes", "Max", false)
        };
        _bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(Arg.Any<GetMeterDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MeterDefinitionDto>>(meters));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<MeterDefinitionDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<MeterDefinitionDto>>().Subject;
        responses.Should().HaveCount(2);
        responses[0].Code.Should().Be("api-calls");
        responses[1].Code.Should().Be("storage");
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(Arg.Any<GetMeterDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MeterDefinitionDto>>([]));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<MeterDefinitionDto> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<MeterDefinitionDto>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(Arg.Any<GetMeterDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<MeterDefinitionDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetAll_InvokesCorrectQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(Arg.Any<GetMeterDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MeterDefinitionDto>>([]));

        await _controller.GetAll(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<MeterDefinitionDto>>>(
            Arg.Any<GetMeterDefinitionsQuery>(),
            Arg.Any<CancellationToken>());
    }
}
