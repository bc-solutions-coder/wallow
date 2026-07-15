using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class ChannelPreferenceRepositoryTests : RepositoryTestBase
{
    private readonly ChannelPreferenceRepository _repository;

    public ChannelPreferenceRepositoryTests()
    {
        _repository = new ChannelPreferenceRepository(Context);
    }

    private ChannelPreference CreatePreference(
        Guid? userId = null,
        ChannelType channelType = ChannelType.Email,
        string notificationType = "TaskAssigned",
        bool isEnabled = true)
    {
        ChannelPreference preference = ChannelPreference.Create(
            userId ?? Guid.NewGuid(),
            channelType,
            notificationType,
            TimeProvider.System,
            isEnabled);
        preference.ClearDomainEvents();
        return preference;
    }

    private async Task AddWithTenantAsync(ChannelPreference preference)
    {
        _repository.Add(preference);
        SetTenantId(preference);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Add_And_GetByUserChannelAndNotificationTypeAsync_ReturnsPreference()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference preference = CreatePreference(userId: userId, channelType: ChannelType.Sms, notificationType: "SystemAlert");

        await AddWithTenantAsync(preference);

        ChannelPreference? result = await _repository.GetByUserChannelAndNotificationTypeAsync(
            userId, ChannelType.Sms, "SystemAlert");

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.ChannelType.Should().Be(ChannelType.Sms);
        result.NotificationType.Should().Be("SystemAlert");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserChannelAndNotificationTypeAsync_WhenNotFound_ReturnsNull()
    {
        ChannelPreference? result = await _repository.GetByUserChannelAndNotificationTypeAsync(
            Guid.NewGuid(), ChannelType.Email, "NonExistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllUserPreferences()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference pref1 = CreatePreference(userId: userId, channelType: ChannelType.Email, notificationType: "Type1");
        ChannelPreference pref2 = CreatePreference(userId: userId, channelType: ChannelType.Sms, notificationType: "Type2");
        ChannelPreference otherPref = CreatePreference(userId: Guid.NewGuid(), notificationType: "Type3");

        await AddWithTenantAsync(pref1);
        await AddWithTenantAsync(pref2);
        await AddWithTenantAsync(otherPref);

        IReadOnlyList<ChannelPreference> result = await _repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenNoPreferences_ReturnsEmpty()
    {
        IReadOnlyList<ChannelPreference> result = await _repository.GetByUserIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference preference = CreatePreference(userId: userId, notificationType: "SaveTest");
        _repository.Add(preference);
        SetTenantId(preference);

        await _repository.SaveChangesAsync();

        ChannelPreference? result = await _repository.GetByUserChannelAndNotificationTypeAsync(
            userId, ChannelType.Email, "SaveTest");
        result.Should().NotBeNull();
    }
}
