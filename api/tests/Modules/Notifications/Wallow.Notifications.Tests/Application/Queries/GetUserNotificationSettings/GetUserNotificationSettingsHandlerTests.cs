using Wallow.Notifications.Application.Channels.Preferences.DTOs;
using Wallow.Notifications.Application.Channels.Preferences.Queries.GetUserNotificationSettings;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Queries.GetUserNotificationSettings;

public class GetUserNotificationSettingsHandlerTests
{
    private readonly IChannelPreferenceRepository _preferenceRepository = Substitute.For<IChannelPreferenceRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetUserNotificationSettingsHandler _handler;

    public GetUserNotificationSettingsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new GetUserNotificationSettingsHandler(_preferenceRepository);
    }

    [Fact]
    public async Task Handle_NoPreferences_ReturnsEmptySettings()
    {
        Guid userId = Guid.NewGuid();
        GetUserNotificationSettingsQuery query = new(userId);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ChannelPreference>());

        Result<UserNotificationSettingsDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.ChannelSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithGlobalPreference_ReturnsGloballyEnabledStatus()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Email, "*", _timeProvider, true);
        GetUserNotificationSettingsQuery query = new(userId);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference> { globalPref });

        Result<UserNotificationSettingsDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelSettings.Should().HaveCount(1);
        result.Value.ChannelSettings[0].ChannelType.Should().Be(ChannelType.Email);
        result.Value.ChannelSettings[0].IsGloballyEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDisabledGlobalPreference_ReturnsGloballyDisabled()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Email, "*", _timeProvider, false);
        GetUserNotificationSettingsQuery query = new(userId);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference> { globalPref });

        Result<UserNotificationSettingsDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelSettings[0].IsGloballyEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithTypeSpecificPreferences_ReturnsTypePreferences()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Email, "*", _timeProvider, true);
        ChannelPreference typePref = ChannelPreference.Create(userId, ChannelType.Email, "billing", _timeProvider, false);
        GetUserNotificationSettingsQuery query = new(userId);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference> { globalPref, typePref });

        Result<UserNotificationSettingsDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelSettings.Should().HaveCount(1);
        result.Value.ChannelSettings[0].TypePreferences.Should().HaveCount(1);
        result.Value.ChannelSettings[0].TypePreferences[0].NotificationType.Should().Be("billing");
        result.Value.ChannelSettings[0].TypePreferences[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleChannels_GroupsByChannelType()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference emailPref = ChannelPreference.Create(userId, ChannelType.Email, "*", _timeProvider, true);
        ChannelPreference smsPref = ChannelPreference.Create(userId, ChannelType.Sms, "*", _timeProvider, false);
        GetUserNotificationSettingsQuery query = new(userId);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference> { emailPref, smsPref });

        Result<UserNotificationSettingsDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelSettings.Should().HaveCount(2);
    }
}
