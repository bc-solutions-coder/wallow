using Foundry.Communications.Domain.Channels.Push.Entities;
using Foundry.Communications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Time.Testing;

namespace Foundry.Communications.Tests.Channels.Push.Domain.Entities;

public class TenantPushConfigurationCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsEnabledConfiguration()
    {
        FakeTimeProvider timeProvider = new();
        TenantId tenantId = TenantId.New();
        PushPlatform platform = PushPlatform.Fcm;
        string credentials = "encrypted-credentials";

        TenantPushConfiguration config = TenantPushConfiguration.Create(tenantId, platform, credentials, timeProvider);

        config.TenantId.Should().Be(tenantId);
        config.Platform.Should().Be(platform);
        config.EncryptedCredentials.Should().Be(credentials);
        config.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceCredentials_ThrowsArgumentException(string? credentials)
    {
        FakeTimeProvider timeProvider = new();

        FluentActions.Invoking(() => TenantPushConfiguration.Create(TenantId.New(), PushPlatform.Fcm, credentials!, timeProvider))
            .Should().Throw<ArgumentException>();
    }
}

public class TenantPushConfigurationEnableDisableTests
{
    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        FakeTimeProvider timeProvider = new();
        TenantPushConfiguration config = TenantPushConfiguration.Create(TenantId.New(), PushPlatform.Apns, "encrypted", timeProvider);

        config.Disable(timeProvider);

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_AfterDisable_SetsIsEnabledToTrue()
    {
        FakeTimeProvider timeProvider = new();
        TenantPushConfiguration config = TenantPushConfiguration.Create(TenantId.New(), PushPlatform.Apns, "encrypted", timeProvider);

        config.Disable(timeProvider);
        config.Enable(timeProvider);

        config.IsEnabled.Should().BeTrue();
    }
}

public class TenantPushConfigurationUpdateCredentialsTests
{
    [Fact]
    public void UpdateCredentials_WithValidCredentials_StoresNewCredentials()
    {
        FakeTimeProvider timeProvider = new();
        TenantPushConfiguration config = TenantPushConfiguration.Create(TenantId.New(), PushPlatform.WebPush, "old-credentials", timeProvider);
        string newCredentials = "new-encrypted-credentials";

        config.UpdateCredentials(newCredentials, timeProvider);

        config.EncryptedCredentials.Should().Be(newCredentials);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateCredentials_WithNullOrWhitespaceCredentials_ThrowsArgumentException(string? credentials)
    {
        FakeTimeProvider timeProvider = new();
        TenantPushConfiguration config = TenantPushConfiguration.Create(TenantId.New(), PushPlatform.Fcm, "valid-credentials", timeProvider);

        FluentActions.Invoking(() => config.UpdateCredentials(credentials!, timeProvider))
            .Should().Throw<ArgumentException>();
    }
}
