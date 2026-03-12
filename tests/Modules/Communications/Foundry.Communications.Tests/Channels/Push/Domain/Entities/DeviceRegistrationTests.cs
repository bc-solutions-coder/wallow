using Foundry.Communications.Domain.Channels.Push;
using Foundry.Communications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Channels.Push.Domain.Entities;

public class DeviceRegistrationRegisterTests
{
    [Fact]
    public void Register_WithValidData_ReturnsActiveDeviceRegistration()
    {
        UserId userId = UserId.New();
        TenantId tenantId = TenantId.New();
        PushPlatform platform = PushPlatform.Fcm;
        string token = "fcm-token-abc123";
        DateTimeOffset registeredAt = DateTimeOffset.UtcNow;

        DeviceRegistration registration = DeviceRegistration.Register(userId, tenantId, platform, token, registeredAt);

        registration.IsActive.Should().BeTrue();
        registration.UserId.Should().Be(userId);
        registration.TenantId.Should().Be(tenantId);
        registration.Platform.Should().Be(platform);
        registration.Token.Should().Be(token);
        registration.RegisteredAt.Should().Be(registeredAt);
    }
}

public class DeviceRegistrationDeactivateTests
{
    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            UserId.New(), TenantId.New(), PushPlatform.Apns, "apns-token-xyz", DateTimeOffset.UtcNow);

        registration.Deactivate();

        registration.IsActive.Should().BeFalse();
    }
}
