using Foundry.Notifications.Api.Contracts.Push;
using Foundry.Notifications.Api.Controllers;
using Foundry.Notifications.Application.Channels.Push.DTOs;
using Foundry.Notifications.Application.Channels.Push.Queries.GetUserDevices;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Notifications.Tests.Api;

public class PushDevicesControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly PushDevicesController _controller;

    public PushDevicesControllerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.New());
        _controller = new PushDevicesController(_bus, _currentUserService, _tenantContext);
    }

    [Fact]
    public async Task RegisterDevice_WhenSuccess_ReturnsNoContent()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        RegisterDeviceRequest request = new(PushPlatform.Fcm, "device-token");
        IActionResult response = await _controller.RegisterDevice(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RegisterDevice_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        RegisterDeviceRequest request = new(PushPlatform.Fcm, "device-token");
        IActionResult response = await _controller.RegisterDevice(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task DeregisterDevice_WhenSuccess_ReturnsNoContent()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult response = await _controller.DeregisterDevice(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeregisterDevice_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.DeregisterDevice(Guid.NewGuid(), CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetUserDevices_WhenSuccess_ReturnsOkWithDevices()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        DeviceRegistrationDto dto = new(Guid.NewGuid(), userId, PushPlatform.Fcm, "token", true, DateTimeOffset.UtcNow);
        IReadOnlyList<DeviceRegistrationDto> devices = new List<DeviceRegistrationDto> { dto };

        _bus.InvokeAsync<Result<IReadOnlyList<DeviceRegistrationDto>>>(
            Arg.Any<GetUserDevicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(devices));

        IActionResult response = await _controller.GetUserDevices(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserDevices_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.GetUserDevices(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task SendPush_WhenSuccess_ReturnsNoContent()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        SendPushRequest request = new(Guid.NewGuid(), "Title", "Body", "Alert");
        IActionResult response = await _controller.SendPush(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SendPush_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        SendPushRequest request = new(Guid.NewGuid(), "Title", "Body", "Alert");
        IActionResult response = await _controller.SendPush(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task RegisterDevice_WhenFailure_ReturnsError()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        Error error = new("Device.NotFound", "Device not found");
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        RegisterDeviceRequest request = new(PushPlatform.Fcm, "device-token");
        IActionResult response = await _controller.RegisterDevice(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeregisterDevice_WhenFailure_ReturnsError()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        Error error = new("Device.NotFound", "Device not found");
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        IActionResult response = await _controller.DeregisterDevice(Guid.NewGuid(), CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetUserDevices_WhenFailure_ReturnsError()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        Error error = new("Device.NotFound", "Devices not found");
        _bus.InvokeAsync<Result<IReadOnlyList<DeviceRegistrationDto>>>(
            Arg.Any<GetUserDevicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<DeviceRegistrationDto>>(error));

        IActionResult response = await _controller.GetUserDevices(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task SendPush_WhenFailure_ReturnsError()
    {
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        Error error = new("Device.NotFound", "Device not found");
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        SendPushRequest request = new(Guid.NewGuid(), "Title", "Body", "Alert");
        IActionResult response = await _controller.SendPush(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }
}
