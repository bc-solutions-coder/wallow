using Wallow.Notifications.Api.Contracts.Preferences;
using Wallow.Notifications.Api.Controllers;
using Wallow.Notifications.Application.Channels.Preferences.DTOs;
using Wallow.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Notifications.Tests.Api;

public class UserNotificationSettingsControllerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly UserNotificationSettingsController _controller;

    public UserNotificationSettingsControllerTests()
    {
        _controller = new UserNotificationSettingsController(_bus, _currentUserService);
    }

    [Fact]
    public async Task GetUserNotificationSettings_WhenUserFound_ReturnsOk()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        UserNotificationSettingsDto dto = new(userId, new List<ChannelSettingDto>());

        _bus.InvokeAsync<Result<UserNotificationSettingsDto>>(
            Arg.Any<GetUserNotificationSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult response = await _controller.GetUserNotificationSettings(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserNotificationSettings_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult response = await _controller.GetUserNotificationSettings(CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task SetChannelEnabled_WhenSuccess_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        SetChannelEnabledRequest request = new(ChannelType.Email, true);
        IActionResult response = await _controller.SetChannelEnabled(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SetChannelEnabled_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        SetChannelEnabledRequest request = new(ChannelType.Email, true);
        IActionResult response = await _controller.SetChannelEnabled(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task SetNotificationTypeEnabled_WhenSuccess_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        SetNotificationTypeEnabledRequest request = new(ChannelType.Push, "Alert", true);
        IActionResult response = await _controller.SetNotificationTypeEnabled(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SetNotificationTypeEnabled_WhenNoUserId_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        SetNotificationTypeEnabledRequest request = new(ChannelType.Push, "Alert", true);
        IActionResult response = await _controller.SetNotificationTypeEnabled(request, CancellationToken.None);

        ObjectResult problemResult = response.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(401);
    }
}
