using Foundry.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class UpsertTenantPushConfigHandlerTests
{
    private readonly ITenantPushConfigurationRepository _configRepository = Substitute.For<ITenantPushConfigurationRepository>();
    private readonly IPushCredentialEncryptor _credentialEncryptor = Substitute.For<IPushCredentialEncryptor>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly UpsertTenantPushConfigHandler _handler;

    public UpsertTenantPushConfigHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _credentialEncryptor.Encrypt(Arg.Any<string>()).Returns("encrypted-creds");
        _handler = new UpsertTenantPushConfigHandler(_configRepository, _credentialEncryptor, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenConfigDoesNotExist_CreatesNewConfig()
    {
        TenantId tenantId = TenantId.New();
        UpsertTenantPushConfigCommand command = new(tenantId, PushPlatform.Fcm, "raw-credentials");

        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns((TenantPushConfiguration?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _configRepository.Received(1).UpsertAsync(Arg.Any<TenantPushConfiguration>(), Arg.Any<CancellationToken>());
        _credentialEncryptor.Received(1).Encrypt("raw-credentials");
    }

    [Fact]
    public async Task Handle_WhenConfigExists_UpdatesCredentials()
    {
        TenantId tenantId = TenantId.New();
        TenantPushConfiguration existing = TenantPushConfiguration.Create(
            tenantId, PushPlatform.Apns, "old-encrypted", _timeProvider);

        UpsertTenantPushConfigCommand command = new(tenantId, PushPlatform.Apns, "new-credentials");

        _configRepository
            .GetByPlatformAsync(PushPlatform.Apns, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.EncryptedCredentials.Should().Be("encrypted-creds");
        await _configRepository.Received(1).UpsertAsync(existing, Arg.Any<CancellationToken>());
    }
}
