using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Domain.Channels.Push;

public sealed class DeviceRegistration : Entity<DeviceRegistrationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public UserId UserId { get; private set; }
    public PushPlatform Platform { get; private set; }
    public string Token { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private DeviceRegistration() { } // EF Core

    private DeviceRegistration(
        UserId userId,
        TenantId tenantId,
        PushPlatform platform,
        string token,
        DateTimeOffset registeredAt)
        : base(DeviceRegistrationId.New())
    {
        UserId = userId;
        TenantId = tenantId;
        Platform = platform;
        Token = token;
        IsActive = true;
        RegisteredAt = registeredAt;
    }

    public static DeviceRegistration Register(
        UserId userId,
        TenantId tenantId,
        PushPlatform platform,
        string token,
        DateTimeOffset registeredAt)
    {
        return new DeviceRegistration(userId, tenantId, platform, token, registeredAt);
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
