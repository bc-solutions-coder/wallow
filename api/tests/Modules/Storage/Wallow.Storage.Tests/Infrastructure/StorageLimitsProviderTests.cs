using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Settings;

namespace Wallow.Storage.Tests.Infrastructure;

public class StorageLimitsProviderTests
{
    private static readonly Guid _tenantGuid = Guid.NewGuid();
    private static readonly TenantId _tenantId = TenantId.Create(_tenantGuid);

    private readonly ITenantSettingRepository<StorageDbContext> _tenantSettings;
    private readonly StorageLimitsProvider _provider;

    public StorageLimitsProviderTests()
    {
        _tenantSettings = Substitute.For<ITenantSettingRepository<StorageDbContext>>();
        _tenantSettings.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _provider = new StorageLimitsProvider(_tenantSettings);
    }

    [Fact]
    public async Task GetLimitsAsync_WhenNoTenantOverrides_ReturnsCodeDefaults()
    {
        StorageLimits limits = await _provider.GetLimitsAsync(_tenantGuid, CancellationToken.None);

        limits.MaxUploadSizeBytes.Should().Be(50L * 1024 * 1024);
        limits.QuotaBytes.Should().Be(1024L * 1024 * 1024);
        limits.IsExtensionAllowed(".anything").Should().BeTrue();
    }

    [Fact]
    public async Task GetLimitsAsync_ReadsTheStorageModuleSettingsForTheTenant()
    {
        await _provider.GetLimitsAsync(_tenantGuid, CancellationToken.None);

        await _tenantSettings.Received(1).GetAllAsync(_tenantId, "storage", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLimitsAsync_UsesTenantOverrides()
    {
        _tenantSettings.GetAllAsync(_tenantId, "storage", Arg.Any<CancellationToken>())
            .Returns([
                new TenantSettingEntity(_tenantId, "storage", "storage.max_upload_size_mb", "10"),
                new TenantSettingEntity(_tenantId, "storage", "storage.allowed_file_types", "jpg"),
                new TenantSettingEntity(_tenantId, "storage", "storage.storage_quota_mb", "100"),
            ]);

        StorageLimits limits = await _provider.GetLimitsAsync(_tenantGuid, CancellationToken.None);

        limits.MaxUploadSizeBytes.Should().Be(10L * 1024 * 1024);
        limits.QuotaBytes.Should().Be(100L * 1024 * 1024);
        limits.IsExtensionAllowed(".jpg").Should().BeTrue();
        limits.IsExtensionAllowed(".png").Should().BeFalse();
    }

    [Fact]
    public async Task GetLimitsAsync_WhenNumericOverrideIsMalformed_FallsBackToDefault()
    {
        _tenantSettings.GetAllAsync(_tenantId, "storage", Arg.Any<CancellationToken>())
            .Returns([
                new TenantSettingEntity(_tenantId, "storage", "storage.max_upload_size_mb", "not-a-number"),
            ]);

        StorageLimits limits = await _provider.GetLimitsAsync(_tenantGuid, CancellationToken.None);

        limits.MaxUploadSizeBytes.Should().Be(50L * 1024 * 1024);
    }
}
