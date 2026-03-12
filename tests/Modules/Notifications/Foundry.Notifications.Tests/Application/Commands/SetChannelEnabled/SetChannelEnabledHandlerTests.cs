using Foundry.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.SetChannelEnabled;

public class SetChannelEnabledHandlerTests
{
    private readonly IChannelPreferenceRepository _preferenceRepository = Substitute.For<IChannelPreferenceRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SetChannelEnabledHandler _handler;

    public SetChannelEnabledHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new SetChannelEnabledHandler(_preferenceRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_PreferenceDoesNotExist_CreatesNewPreference()
    {
        SetChannelEnabledCommand command = new(
            UserId: Guid.NewGuid(),
            ChannelType: ChannelType.Email,
            IsEnabled: true,
            NotificationType: "*");

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(command.UserId, command.ChannelType, command.NotificationType, Arg.Any<CancellationToken>())
            .Returns((ChannelPreference?)null);

        Result<Notifications.Application.Preferences.DTOs.ChannelPreferenceDto> result =
            await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(command.UserId);
        result.Value.ChannelType.Should().Be(ChannelType.Email);
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.Received(1).Add(Arg.Any<ChannelPreference>());
        await _preferenceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PreferenceExists_EnablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference existing = ChannelPreference.Create(userId, ChannelType.Email, "*", _timeProvider, false);

        SetChannelEnabledCommand command = new(
            UserId: userId,
            ChannelType: ChannelType.Email,
            IsEnabled: true,
            NotificationType: "*");

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Email, "*", Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<Notifications.Application.Preferences.DTOs.ChannelPreferenceDto> result =
            await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.DidNotReceive().Add(Arg.Any<ChannelPreference>());
        await _preferenceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PreferenceExists_DisablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference existing = ChannelPreference.Create(userId, ChannelType.Sms, "*", _timeProvider, true);

        SetChannelEnabledCommand command = new(
            UserId: userId,
            ChannelType: ChannelType.Sms,
            IsEnabled: false,
            NotificationType: "*");

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Sms, "*", Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<Notifications.Application.Preferences.DTOs.ChannelPreferenceDto> result =
            await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }
}
