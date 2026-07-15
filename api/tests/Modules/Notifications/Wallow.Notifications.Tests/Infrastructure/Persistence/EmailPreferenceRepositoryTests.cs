using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Enums;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class EmailPreferenceRepositoryTests : RepositoryTestBase
{
    private readonly EmailPreferenceRepository _repository;

    public EmailPreferenceRepositoryTests()
    {
        _repository = new EmailPreferenceRepository(Context);
    }

    private EmailPreference CreatePreference(
        Guid? userId = null,
        NotificationType notificationType = NotificationType.SystemAlert,
        bool isEnabled = true)
    {
        EmailPreference preference = EmailPreference.Create(
            userId ?? Guid.NewGuid(),
            notificationType,
            isEnabled,
            TimeProvider.System);
        return preference;
    }

    private async Task AddWithTenantAsync(EmailPreference preference)
    {
        _repository.Add(preference);
        SetTenantId(preference);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Add_PersistsEmailPreference()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = CreatePreference(userId: userId);

        await AddWithTenantAsync(preference);

        EmailPreference? result = await Context.EmailPreferences.FindAsync(preference.Id);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByUserAndTypeAsync_ReturnsMatchingPreference()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = CreatePreference(userId: userId, notificationType: NotificationType.Mention);

        await AddWithTenantAsync(preference);

        EmailPreference? result = await _repository.GetByUserAndTypeAsync(userId, NotificationType.Mention);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.NotificationType.Should().Be(NotificationType.Mention);
    }

    [Fact]
    public async Task GetByUserAndTypeAsync_WhenNotFound_ReturnsNull()
    {
        EmailPreference? result = await _repository.GetByUserAndTypeAsync(Guid.NewGuid(), NotificationType.SystemAlert);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllPreferencesForUser()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference pref1 = CreatePreference(userId: userId, notificationType: NotificationType.SystemAlert);
        EmailPreference pref2 = CreatePreference(userId: userId, notificationType: NotificationType.Mention);
        EmailPreference otherPref = CreatePreference(userId: Guid.NewGuid(), notificationType: NotificationType.Mention);

        await AddWithTenantAsync(pref1);
        await AddWithTenantAsync(pref2);
        await AddWithTenantAsync(otherPref);

        IReadOnlyList<EmailPreference> result = await _repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenNoPreferences_ReturnsEmpty()
    {
        IReadOnlyList<EmailPreference> result = await _repository.GetByUserIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = CreatePreference(userId: userId, notificationType: NotificationType.Announcement);
        _repository.Add(preference);
        SetTenantId(preference);

        await _repository.SaveChangesAsync();

        EmailPreference? result = await Context.EmailPreferences.FindAsync(preference.Id);
        result.Should().NotBeNull();
    }
}
