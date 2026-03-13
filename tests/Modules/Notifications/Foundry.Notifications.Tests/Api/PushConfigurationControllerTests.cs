using Foundry.Notifications.Api.Contracts.Push;
using Foundry.Notifications.Api.Controllers;
using Foundry.Notifications.Application.Channels.Push.DTOs;
using Foundry.Notifications.Application.Channels.Push.Queries.GetTenantPushConfig;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Notifications.Tests.Api;

public class PushConfigurationControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly PushConfigurationController _controller;

    public PushConfigurationControllerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.New());
        _controller = new PushConfigurationController(_bus, _tenantContext);
    }

    [Fact]
    public async Task GetTenantPushConfig_WhenConfigExists_ReturnsOk()
    {
        TenantPushConfigDto dto = new(Guid.NewGuid(), Guid.NewGuid(), PushPlatform.Fcm, "[redacted]", true);

        _bus.InvokeAsync<Result<TenantPushConfigDto?>>(
            Arg.Any<GetTenantPushConfigQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<TenantPushConfigDto?>(dto));

        IActionResult response = await _controller.GetTenantPushConfig(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetTenantPushConfig_WhenNoConfig_ReturnsNoContent()
    {
        _bus.InvokeAsync<Result<TenantPushConfigDto?>>(
            Arg.Any<GetTenantPushConfigQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<TenantPushConfigDto?>(null));

        IActionResult response = await _controller.GetTenantPushConfig(CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpsertTenantPushConfig_WhenSuccess_ReturnsNoContent()
    {
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        UpsertTenantPushConfigRequest request = new(PushPlatform.Fcm, "credentials");
        IActionResult response = await _controller.UpsertTenantPushConfig(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SetTenantPushEnabled_WhenSuccess_ReturnsNoContent()
    {
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        SetTenantPushEnabledRequest request = new(PushPlatform.Fcm, true);
        IActionResult response = await _controller.SetTenantPushEnabled(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveTenantPushConfig_WhenSuccess_ReturnsNoContent()
    {
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult response = await _controller.RemoveTenantPushConfig(PushPlatform.Fcm, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetTenantPushConfig_WhenFailure_ReturnsError()
    {
        Error error = new("PushConfig.NotFound", "Push config not found");

        _bus.InvokeAsync<Result<TenantPushConfigDto?>>(
            Arg.Any<GetTenantPushConfigQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TenantPushConfigDto?>(error));

        IActionResult response = await _controller.GetTenantPushConfig(CancellationToken.None);

        ObjectResult objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpsertTenantPushConfig_WhenFailure_ReturnsError()
    {
        Error error = new("Validation.InvalidCredentials", "Invalid credentials");

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        UpsertTenantPushConfigRequest request = new(PushPlatform.Fcm, "bad-creds");
        IActionResult response = await _controller.UpsertTenantPushConfig(request, CancellationToken.None);

        ObjectResult objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SetTenantPushEnabled_WhenFailure_ReturnsError()
    {
        Error error = new("PushConfig.NotFound", "Push config not found");

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        SetTenantPushEnabledRequest request = new(PushPlatform.Fcm, true);
        IActionResult response = await _controller.SetTenantPushEnabled(request, CancellationToken.None);

        ObjectResult objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RemoveTenantPushConfig_WhenFailure_ReturnsError()
    {
        Error error = new("PushConfig.NotFound", "Push config not found");

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        IActionResult response = await _controller.RemoveTenantPushConfig(PushPlatform.Fcm, CancellationToken.None);

        ObjectResult objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }
}
