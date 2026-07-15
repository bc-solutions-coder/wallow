using Wallow.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class SetTenantPushEnabledHandlerTests
{
    private readonly ITenantPushConfigurationRepository _configRepository = Substitute.For<ITenantPushConfigurationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SetTenantPushEnabledHandler _handler;

    public SetTenantPushEnabledHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new SetTenantPushEnabledHandler(_configRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenConfigFound_EnablesAndUpserts()
    {
        TenantId tenantId = TenantId.New();
        TenantPushConfiguration config = TenantPushConfiguration.Create(tenantId, PushPlatform.Fcm, "creds", _timeProvider);
        config.Disable(_timeProvider);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns(config);

        SetTenantPushEnabledCommand command = new(tenantId, PushPlatform.Fcm, IsEnabled: true);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        config.IsEnabled.Should().BeTrue();
        await _configRepository.Received(1).UpsertAsync(config, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConfigFound_DisablesAndUpserts()
    {
        TenantId tenantId = TenantId.New();
        TenantPushConfiguration config = TenantPushConfiguration.Create(tenantId, PushPlatform.Apns, "creds", _timeProvider);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Apns, Arg.Any<CancellationToken>())
            .Returns(config);

        SetTenantPushEnabledCommand command = new(tenantId, PushPlatform.Apns, IsEnabled: false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenConfigNotFound_ReturnsNotFoundFailure()
    {
        _configRepository
            .GetByPlatformAsync(Arg.Any<PushPlatform>(), Arg.Any<CancellationToken>())
            .Returns((TenantPushConfiguration?)null);

        SetTenantPushEnabledCommand command = new(TenantId.New(), PushPlatform.Fcm, IsEnabled: true);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
