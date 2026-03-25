using Wallow.Notifications.Api.Contracts.Preferences;
using Wallow.Notifications.Api.Controllers;
using Wallow.Notifications.Application.Channels.Preferences.DTOs;
using Wallow.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;
using Wallow.Notifications.Application.Preferences.DTOs;
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
    public async Task GetUserNotificationSettings_WhenUserFoundWithChannelSettings_ReturnsMappedResponse()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        List<ChannelPreferenceDto> preferences =
        [
            new(Guid.NewGuid(), userId, ChannelType.Email, "Alert", true, DateTime.UtcNow, null)
        ];
        List<ChannelSettingDto> channelSettings =
        [
            new(ChannelType.Email, true, preferences)
        ];
        UserNotificationSettingsDto dto = new(userId, channelSettings);

        _bus.InvokeAsync<Result<UserNotificationSettingsDto>>(
            Arg.Any<GetUserNotificationSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult response = await _controller.GetUserNotificationSettings(CancellationToken.None);

        OkObjectResult okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        UserNotificationSettingsResponse settingsResponse = okResult.Value.Should().BeOfType<UserNotificationSettingsResponse>().Subject;
        settingsResponse.UserId.Should().Be(userId);
        settingsResponse.ChannelSettings.Should().HaveCount(1);
        settingsResponse.ChannelSettings[0].ChannelType.Should().Be(ChannelType.Email);
        settingsResponse.ChannelSettings[0].IsGloballyEnabled.Should().BeTrue();
        settingsResponse.ChannelSettings[0].TypePreferences.Should().HaveCount(1);
        settingsResponse.ChannelSettings[0].TypePreferences[0].NotificationType.Should().Be("Alert");
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
    public async Task GetUserNotificationSettings_WhenResultIsFailure_ReturnsError()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = new("Settings.NotFound", "Settings not found");
        _bus.InvokeAsync<Result<UserNotificationSettingsDto>>(
            Arg.Any<GetUserNotificationSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserNotificationSettingsDto>(error));

        IActionResult response = await _controller.GetUserNotificationSettings(CancellationToken.None);

        ObjectResult errorResult = response.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(404);
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
    public async Task SetChannelEnabled_WhenResultIsFailure_ReturnsError()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = new("Validation.InvalidChannel", "Invalid channel type");
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        SetChannelEnabledRequest request = new(ChannelType.Email, true);
        IActionResult response = await _controller.SetChannelEnabled(request, CancellationToken.None);

        ObjectResult errorResult = response.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(400);
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

    [Fact]
    public async Task SetNotificationTypeEnabled_WhenResultIsFailure_ReturnsError()
    {
        Guid userId = Guid.NewGuid();
        _currentUserService.GetCurrentUserId().Returns(userId);

        Error error = new("Validation.InvalidType", "Invalid notification type");
        _bus.InvokeAsync<Result>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        SetNotificationTypeEnabledRequest request = new(ChannelType.Push, "Alert", true);
        IActionResult response = await _controller.SetNotificationTypeEnabled(request, CancellationToken.None);

        ObjectResult errorResult = response.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(400);
    }
}
