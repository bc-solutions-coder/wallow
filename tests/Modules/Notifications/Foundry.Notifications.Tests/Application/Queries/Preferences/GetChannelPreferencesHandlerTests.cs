using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Application.Preferences.Queries;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Queries.Preferences;

public class GetChannelPreferencesHandlerTests
{
    private readonly IChannelPreferenceRepository _preferenceRepository = Substitute.For<IChannelPreferenceRepository>();
    private readonly GetChannelPreferencesHandler _handler;

    public GetChannelPreferencesHandlerTests()
    {
        _handler = new GetChannelPreferencesHandler(_preferenceRepository);
    }

    [Fact]
    public async Task Handle_ReturnsAllPreferencesForUser()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference pref1 = ChannelPreference.Create(userId, ChannelType.Email, "*", TimeProvider.System, true);
        ChannelPreference pref2 = ChannelPreference.Create(userId, ChannelType.Push, "Alert", TimeProvider.System, false);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference> { pref1, pref2 });

        Result<IReadOnlyList<ChannelPreferenceDto>> result = await _handler.Handle(
            new GetChannelPreferencesQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNoPreferences_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelPreference>());

        Result<IReadOnlyList<ChannelPreferenceDto>> result = await _handler.Handle(
            new GetChannelPreferencesQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
