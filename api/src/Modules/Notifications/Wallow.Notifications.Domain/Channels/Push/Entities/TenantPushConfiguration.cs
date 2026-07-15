using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Domain.Channels.Push.Entities;

public sealed class TenantPushConfiguration : AggregateRoot<TenantPushConfigurationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public PushPlatform Platform { get; private set; }
    public string EncryptedCredentials { get; private set; } = null!;
    public bool IsEnabled { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private TenantPushConfiguration() { } // EF Core

    private TenantPushConfiguration(
        TenantId tenantId,
        PushPlatform platform,
        string encryptedCredentials,
        TimeProvider timeProvider)
        : base(TenantPushConfigurationId.New())
    {
        TenantId = tenantId;
        Platform = platform;
        EncryptedCredentials = encryptedCredentials;
        IsEnabled = true;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static TenantPushConfiguration Create(
        TenantId tenantId,
        PushPlatform platform,
        string encryptedCredentials,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);

        return new TenantPushConfiguration(tenantId, platform, encryptedCredentials, timeProvider);
    }

    public void UpdateCredentials(string encryptedCredentials, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);

        EncryptedCredentials = encryptedCredentials;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Enable(TimeProvider timeProvider)
    {
        IsEnabled = true;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Disable(TimeProvider timeProvider)
    {
        IsEnabled = false;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
