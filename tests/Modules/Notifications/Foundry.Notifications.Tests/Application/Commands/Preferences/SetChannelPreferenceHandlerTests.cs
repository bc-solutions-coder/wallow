using Foundry.Notifications.Application.Preferences.Commands;
using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Preferences;

public class SetChannelPreferenceHandlerTests
{
    private readonly IChannelPreferenceRepository _preferenceRepository = Substitute.For<IChannelPreferenceRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SetChannelPreferenceHandler _handler;

    public SetChannelPreferenceHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new SetChannelPreferenceHandler(_preferenceRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenPreferenceDoesNotExist_CreatesNewPreference()
    {
        Guid userId = Guid.NewGuid();
        SetChannelPreferenceCommand command = new(userId, ChannelType.Email, "*", IsEnabled: true);

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Email, "*", Arg.Any<CancellationToken>())
            .Returns((ChannelPreference?)null);

        Result<ChannelPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.Received(1).Add(Arg.Any<ChannelPreference>());
        await _preferenceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_EnablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference existing = ChannelPreference.Create(userId, ChannelType.Push, "*", _timeProvider, false);
        SetChannelPreferenceCommand command = new(userId, ChannelType.Push, "*", IsEnabled: true);

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Push, "*", Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<ChannelPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.DidNotReceive().Add(Arg.Any<ChannelPreference>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_DisablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference existing = ChannelPreference.Create(userId, ChannelType.Sms, "Alert", _timeProvider, true);
        SetChannelPreferenceCommand command = new(userId, ChannelType.Sms, "Alert", IsEnabled: false);

        _preferenceRepository
            .GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Sms, "Alert", Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<ChannelPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }
}
