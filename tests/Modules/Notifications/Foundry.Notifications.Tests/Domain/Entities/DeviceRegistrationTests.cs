using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Domain.Entities;

public class DeviceRegistrationTests
{
    [Fact]
    public void Register_WithValidData_ReturnsActiveRegistration()
    {
        UserId userId = UserId.New();
        TenantId tenantId = TenantId.New();
        PushPlatform platform = PushPlatform.Fcm;
        string token = "fcm-device-token-abc123";
        DateTimeOffset registeredAt = DateTimeOffset.UtcNow;

        DeviceRegistration registration = DeviceRegistration.Register(
            userId, tenantId, platform, token, registeredAt);

        registration.UserId.Should().Be(userId);
        registration.TenantId.Should().Be(tenantId);
        registration.Platform.Should().Be(platform);
        registration.Token.Should().Be(token);
        registration.IsActive.Should().BeTrue();
        registration.RegisteredAt.Should().Be(registeredAt);
    }

    [Theory]
    [InlineData(PushPlatform.Fcm)]
    [InlineData(PushPlatform.Apns)]
    [InlineData(PushPlatform.WebPush)]
    public void Register_WithDifferentPlatforms_SetsPlatformCorrectly(PushPlatform platform)
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            UserId.New(), TenantId.New(), platform, "token", DateTimeOffset.UtcNow);

        registration.Platform.Should().Be(platform);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            UserId.New(), TenantId.New(), PushPlatform.Apns, "apns-token", DateTimeOffset.UtcNow);

        registration.Deactivate();

        registration.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Register_GeneratesUniqueId()
    {
        DeviceRegistration first = DeviceRegistration.Register(
            UserId.New(), TenantId.New(), PushPlatform.Fcm, "token-1", DateTimeOffset.UtcNow);
        DeviceRegistration second = DeviceRegistration.Register(
            UserId.New(), TenantId.New(), PushPlatform.Fcm, "token-2", DateTimeOffset.UtcNow);

        first.Id.Should().NotBe(second.Id);
    }
}
