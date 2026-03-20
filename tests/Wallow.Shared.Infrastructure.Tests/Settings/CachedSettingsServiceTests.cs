using System.Text.Json;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Settings;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

public class CachedSettingsServiceTests
{
    private readonly ITenantSettingRepository<FakeDbContext> _tenantRepo;
    private readonly IUserSettingRepository<FakeDbContext> _userRepo;
    private readonly ISettingRegistry _registry;
    private readonly IDistributedCache _cache;
    private readonly CachedSettingsService<FakeDbContext> _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CachedSettingsServiceTests()
    {
        _tenantRepo = Substitute.For<ITenantSettingRepository<FakeDbContext>>();
        _userRepo = Substitute.For<IUserSettingRepository<FakeDbContext>>();
        _registry = Substitute.For<ISettingRegistry>();
        _cache = Substitute.For<IDistributedCache>();

        _registry.ModuleName.Returns("test-module");
        _registry.Metadata.Returns(new Dictionary<string, SettingMetadata>
        {
            ["feature.enabled"] = new SettingMetadata(
                Key: "feature.enabled",
                DisplayName: "Feature Enabled",
                Description: "Whether feature is enabled",
                ValueType: typeof(bool),
                DefaultValue: false),
            ["max.retries"] = new SettingMetadata(
                Key: "max.retries",
                DisplayName: "Max Retries",
                Description: "Max retry attempts",
                ValueType: typeof(int),
                DefaultValue: 3)
        });

        _registry.IsCodeDefinedKey("feature.enabled").Returns(true);
        _registry.IsCodeDefinedKey("max.retries").Returns(true);
        _registry.IsCodeDefinedKey("custom.key").Returns(false);
        _registry.IsCodeDefinedKey("unknown.key").Returns(false);

        _sut = new CachedSettingsService<FakeDbContext>(_tenantRepo, _userRepo, _registry, _cache);
    }

    [Fact]
    public async Task GetTenantSettingsAsync_WhenCacheHit_ReturnsCachedValues()
    {
        List<ResolvedSetting> cached = [new ResolvedSetting("feature.enabled", "true", "tenant", "Feature", "Desc", "False")];
        byte[] cachedBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached));
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cachedBytes);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetTenantSettingsAsync(_tenantId);

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("feature.enabled");
        result[0].Value.Should().Be("true");
        await _tenantRepo.DidNotReceive().GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTenantSettingsAsync_WhenCacheMiss_QueriesRepoAndCaches()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        TenantId tid = TenantId.Create(_tenantId);
        TenantSettingEntity entity = new(tid, "test-module", "feature.enabled", "true");
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entity]);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetTenantSettingsAsync(_tenantId);

        result.Should().HaveCount(2); // 2 metadata keys
        ResolvedSetting featureSetting = result.First(r => r.Key == "feature.enabled");
        featureSetting.Value.Should().Be("true");
        featureSetting.Source.Should().Be("tenant");
        await _cache.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTenantSettingsAsync_WhenNoTenantOverride_UsesDefaults()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetTenantSettingsAsync(_tenantId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.Source.Should().Be("default"));
        ResolvedSetting maxRetriesSetting = result.First(r => r.Key == "max.retries");
        maxRetriesSetting.Value.Should().Be("3");
    }

    [Fact]
    public async Task GetTenantSettingsAsync_WithCustomKey_IncludesCustomKeyInResults()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        TenantId tid = TenantId.Create(_tenantId);
        TenantSettingEntity customEntity = new(tid, "test-module", "custom.my-key", "custom-value");
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([customEntity]);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetTenantSettingsAsync(_tenantId);

        ResolvedSetting? customSetting = result.FirstOrDefault(r => r.Key == "custom.my-key");
        customSetting.Should().NotBeNull();
        customSetting!.Value.Should().Be("custom-value");
        customSetting.Source.Should().Be("tenant");
        customSetting.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task GetUserSettingsAsync_WhenCacheHit_ReturnsCachedValues()
    {
        List<ResolvedSetting> cached = [new ResolvedSetting("feature.enabled", "false", "user", "Feature", "Desc", "False")];
        byte[] cachedBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached));
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cachedBytes);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetUserSettingsAsync(_tenantId, _userId);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be("user");
        await _tenantRepo.DidNotReceive().GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserSettingsAsync_WhenCacheMiss_MergesUserOverTenantOverDefault()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        TenantId tid = TenantId.Create(_tenantId);
        string userIdStr = _userId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        TenantSettingEntity tenantEntity = new(tid, "test-module", "feature.enabled", "true");
        UserSettingEntity userEntity = new(tid, userIdStr, "test-module", "max.retries", "10");

        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([tenantEntity]);
        _userRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([userEntity]);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetUserSettingsAsync(_tenantId, _userId);

        ResolvedSetting featureSetting = result.First(r => r.Key == "feature.enabled");
        featureSetting.Value.Should().Be("true");
        featureSetting.Source.Should().Be("tenant");

        ResolvedSetting retriesSetting = result.First(r => r.Key == "max.retries");
        retriesSetting.Value.Should().Be("10");
        retriesSetting.Source.Should().Be("user");
    }

    [Fact]
    public async Task GetUserSettingsAsync_WithUserOnlyCustomKey_IncludesItAsUserSource()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        TenantId tid = TenantId.Create(_tenantId);
        string userIdStr = _userId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        UserSettingEntity userOnlyEntity = new(tid, userIdStr, "test-module", "custom.user-pref", "dark");
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _userRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([userOnlyEntity]);

        IReadOnlyList<ResolvedSetting> result = await _sut.GetUserSettingsAsync(_tenantId, _userId);

        ResolvedSetting? userPref = result.FirstOrDefault(r => r.Key == "custom.user-pref");
        userPref.Should().NotBeNull();
        userPref!.Value.Should().Be("dark");
        userPref.Source.Should().Be("user");
    }

    [Fact]
    public async Task GetConfigAsync_ReturnsDictionaryOfKeyValues()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _userRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ResolvedSettingsConfig config = await _sut.GetConfigAsync(_tenantId, _userId);

        config.Settings.Should().ContainKey("feature.enabled");
        config.Settings.Should().ContainKey("max.retries");
        config.Settings["max.retries"].Should().Be("3");
    }

    [Fact]
    public async Task UpdateTenantSettingsAsync_WithValidKey_UpsertsAndInvalidatesCache()
    {
        _registry.IsCodeDefinedKey("feature.enabled").Returns(true);

        List<SettingUpdate> updates = [new SettingUpdate("feature.enabled", "true")];
        await _sut.UpdateTenantSettingsAsync(_tenantId, updates, _userId);

        await _tenantRepo.Received(1).UpsertAsync(Arg.Any<TenantSettingEntity>(), Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTenantSettingsAsync_WithUnknownKey_ThrowsArgumentException()
    {
        _registry.IsCodeDefinedKey("unknown.bad-key").Returns(false);

        List<SettingUpdate> updates = [new SettingUpdate("unknown.bad-key", "value")];

        Func<Task> act = () => _sut.UpdateTenantSettingsAsync(_tenantId, updates, _userId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Unknown setting key*");
    }

    [Fact]
    public async Task UpdateUserSettingsAsync_WithValidCustomKey_UpsertsAndInvalidatesUserCache()
    {
        List<SettingUpdate> updates = [new SettingUpdate("custom.pref", "value")];
        await _sut.UpdateUserSettingsAsync(_tenantId, _userId, updates);

        await _userRepo.Received(1).UpsertAsync(Arg.Any<UserSettingEntity>(), Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTenantSettingsAsync_WithValidKey_DeletesAndInvalidatesCache()
    {
        List<string> keys = ["feature.enabled"];
        await _sut.DeleteTenantSettingsAsync(_tenantId, keys, _userId);

        await _tenantRepo.Received(1).DeleteAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserSettingsAsync_DeletesAndInvalidatesUserCache()
    {
        List<string> keys = ["custom.pref"];
        await _sut.DeleteUserSettingsAsync(_tenantId, _userId, keys);

        await _userRepo.Received(1).DeleteAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTenantSettingsAsync_WithMultipleSettings_UpsertsAll()
    {
        List<SettingUpdate> updates =
        [
            new SettingUpdate("feature.enabled", "true"),
            new SettingUpdate("max.retries", "5")
        ];

        await _sut.UpdateTenantSettingsAsync(_tenantId, updates, _userId);

        await _tenantRepo.Received(2).UpsertAsync(Arg.Any<TenantSettingEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTenantSettingsAsync_UsesTenantSpecificCacheKey()
    {
        Guid tenantId1 = Guid.NewGuid();
        Guid tenantId2 = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _tenantRepo.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.GetTenantSettingsAsync(tenantId1);
        await _sut.GetTenantSettingsAsync(tenantId2);

        await _cache.Received(1).GetAsync($"settings:test-module:{tenantId1}", Arg.Any<CancellationToken>());
        await _cache.Received(1).GetAsync($"settings:test-module:{tenantId2}", Arg.Any<CancellationToken>());
    }
}
