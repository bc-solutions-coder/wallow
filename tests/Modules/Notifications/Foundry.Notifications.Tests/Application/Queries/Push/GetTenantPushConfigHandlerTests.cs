using Foundry.Notifications.Application.Channels.Push.DTOs;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Application.Channels.Push.Queries.GetTenantPushConfig;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Queries.Push;

public class GetTenantPushConfigHandlerTests
{
    private readonly ITenantPushConfigurationRepository _configRepository = Substitute.For<ITenantPushConfigurationRepository>();
    private readonly GetTenantPushConfigHandler _handler;

    public GetTenantPushConfigHandlerTests()
    {
        _handler = new GetTenantPushConfigHandler(_configRepository);
    }

    [Fact]
    public async Task Handle_WhenConfigExists_ReturnsRedactedDto()
    {
        TenantId tenantId = TenantId.New();
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            tenantId, PushPlatform.Fcm, "actual-secret-creds", TimeProvider.System);

        _configRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Result<TenantPushConfigDto?> result = await _handler.Handle(
            new GetTenantPushConfigQuery(tenantId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EncryptedCredentials.Should().Be("[redacted]");
        result.Value.Platform.Should().Be(PushPlatform.Fcm);
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNoConfig_ReturnsSuccessWithNull()
    {
        _configRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((TenantPushConfiguration?)null);

        Result<TenantPushConfigDto?> result = await _handler.Handle(
            new GetTenantPushConfigQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
