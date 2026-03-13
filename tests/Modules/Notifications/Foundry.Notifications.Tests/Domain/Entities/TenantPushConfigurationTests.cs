using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Domain.Entities;

public class TenantPushConfigurationTests
{
    [Fact]
    public void Create_WithValidData_SetsEnabledByDefault()
    {
        TenantId tenantId = TenantId.New();
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            tenantId, PushPlatform.Fcm, "encrypted-creds", TimeProvider.System);

        config.TenantId.Should().Be(tenantId);
        config.Platform.Should().Be(PushPlatform.Fcm);
        config.EncryptedCredentials.Should().Be("encrypted-creds");
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyCredentials_ThrowsArgumentException()
    {
        Action act = () => TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, string.Empty, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateCredentials_ChangesEncryptedCredentials()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, "old-creds", TimeProvider.System);

        config.UpdateCredentials("new-creds", TimeProvider.System);

        config.EncryptedCredentials.Should().Be("new-creds");
    }

    [Fact]
    public void UpdateCredentials_WithEmpty_ThrowsArgumentException()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Fcm, "creds", TimeProvider.System);

        Action act = () => config.UpdateCredentials(string.Empty, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Apns, "creds", TimeProvider.System);

        config.Disable(TimeProvider.System);

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_SetsIsEnabledTrue()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.WebPush, "creds", TimeProvider.System);
        config.Disable(TimeProvider.System);

        config.Enable(TimeProvider.System);

        config.IsEnabled.Should().BeTrue();
    }
}
