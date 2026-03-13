using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Notifications.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;


namespace Foundry.Notifications.Tests.Infrastructure.Services;

public sealed class PushProviderFactoryTests : IDisposable
{
    private readonly ITenantPushConfigurationRepository _configRepository = Substitute.For<ITenantPushConfigurationRepository>();
    private readonly IPushCredentialEncryptor _encryptor = Substitute.For<IPushCredentialEncryptor>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly HttpClient _httpClient = new();
    private readonly PushProviderFactory _sut;

    public PushProviderFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        _encryptor.Decrypt(Arg.Any<string>()).Returns("decrypted-credentials");
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        _sut = new PushProviderFactory(_configRepository, _encryptor, _httpClientFactory, _loggerFactory);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        (_loggerFactory as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetProvider_WhenConfigIsNull_ReturnsLogPushProvider()
    {
        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(null));

        IPushProvider result = _sut.GetProvider(PushPlatform.Fcm);

        result.Should().BeOfType<LogPushProvider>();
    }

    [Fact]
    public void GetProvider_WhenConfigIsDisabled_ReturnsLogPushProvider()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, "encrypted", TimeProvider.System);
        config.Disable(TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        IPushProvider result = _sut.GetProvider(PushPlatform.Fcm);

        result.Should().BeOfType<LogPushProvider>();
    }

    [Fact]
    public void GetProvider_WhenFcmEnabled_ReturnsFcmPushProvider()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, "encrypted", TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        IPushProvider result = _sut.GetProvider(PushPlatform.Fcm);

        result.Should().BeOfType<FcmPushProvider>();
    }

    [Fact]
    public void GetProvider_WhenApnsEnabled_ReturnsApnsPushProvider()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Apns, "encrypted", TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Apns, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        IPushProvider result = _sut.GetProvider(PushPlatform.Apns);

        result.Should().BeOfType<ApnsPushProvider>();
    }

    [Fact]
    public void GetProvider_WhenWebPushEnabled_ReturnsWebPushPushProvider()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.WebPush, "encrypted", TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.WebPush, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        IPushProvider result = _sut.GetProvider(PushPlatform.WebPush);

        result.Should().BeOfType<WebPushPushProvider>();
    }

    [Fact]
    public void GetProvider_WhenEnabled_DecryptsCredentials()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, "encrypted-blob", TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Fcm, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        _sut.GetProvider(PushPlatform.Fcm);

        _encryptor.Received(1).Decrypt("encrypted-blob");
    }

    [Fact]
    public void GetProvider_WhenEnabled_CreatesNamedHttpClient()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Apns, "encrypted", TimeProvider.System);

        _configRepository
            .GetByPlatformAsync(PushPlatform.Apns, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantPushConfiguration?>(config));

        _sut.GetProvider(PushPlatform.Apns);

        _httpClientFactory.Received(1).CreateClient("Push_Apns");
    }
}
