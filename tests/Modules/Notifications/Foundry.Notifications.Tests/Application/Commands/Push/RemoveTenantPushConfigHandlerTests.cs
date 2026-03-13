using Foundry.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class RemoveTenantPushConfigHandlerTests
{
    private readonly ITenantPushConfigurationRepository _configRepository = Substitute.For<ITenantPushConfigurationRepository>();
    private readonly RemoveTenantPushConfigHandler _handler;

    public RemoveTenantPushConfigHandlerTests()
    {
        _handler = new RemoveTenantPushConfigHandler(_configRepository);
    }

    [Fact]
    public async Task Handle_CallsDeleteByPlatformAndSucceeds()
    {
        TenantId tenantId = TenantId.New();
        RemoveTenantPushConfigCommand command = new(tenantId, PushPlatform.Fcm);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _configRepository.Received(1).DeleteByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>());
    }
}
