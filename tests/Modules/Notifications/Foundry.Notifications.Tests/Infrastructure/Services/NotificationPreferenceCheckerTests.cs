using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;
using Foundry.Notifications.Infrastructure.Persistence;
using Foundry.Notifications.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public sealed class NotificationPreferenceCheckerTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly NotificationPreferenceChecker _checker;
    private readonly UserId _userId = UserId.New();
    private readonly TenantId _tenantId = TenantId.New();

    public NotificationPreferenceCheckerTests()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);

        DbContextOptions<NotificationsDbContext> options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new NotificationsDbContext(options, tenantContext);
        _checker = new NotificationPreferenceChecker(_dbContext);
    }

    private void StampTenantId(ChannelPreference preference)
    {
        _dbContext.ChannelPreferences.Add(preference);
        _dbContext.Entry(preference).Property(p => p.TenantId).CurrentValue = _tenantId;
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenNoPreferencesExist_ReturnsTrue()
    {
        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenGlobalKillSwitchDisabled_ReturnsFalse()
    {
        ChannelPreference preference = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "*", TimeProvider.System, isEnabled: false);
        StampTenantId(preference);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenGlobalKillSwitchEnabled_ReturnsTrue()
    {
        ChannelPreference preference = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "*", TimeProvider.System, isEnabled: true);
        StampTenantId(preference);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenSpecificTypeDisabled_ReturnsFalse()
    {
        ChannelPreference preference = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "order.created", TimeProvider.System, isEnabled: false);
        StampTenantId(preference);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenSpecificTypeEnabled_ReturnsTrue()
    {
        ChannelPreference preference = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "order.created", TimeProvider.System, isEnabled: true);
        StampTenantId(preference);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenGlobalDisabledButSpecificEnabled_ReturnsFalse()
    {
        ChannelPreference globalPref = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "*", TimeProvider.System, isEnabled: false);
        ChannelPreference specificPref = ChannelPreference.Create(
            _userId.Value, ChannelType.Email, "order.created", TimeProvider.System, isEnabled: true);
        StampTenantId(globalPref);
        StampTenantId(specificPref);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WhenDifferentChannelDisabled_ReturnsTrue()
    {
        ChannelPreference preference = ChannelPreference.Create(
            _userId.Value, ChannelType.Sms, "*", TimeProvider.System, isEnabled: false);
        StampTenantId(preference);
        await _dbContext.SaveChangesAsync();

        bool result = await _checker.IsChannelEnabledAsync(
            _userId, ChannelType.Email, "order.created");

        result.Should().BeTrue();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
