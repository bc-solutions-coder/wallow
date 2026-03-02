using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;

namespace Foundry.Communications.Tests.Domain.Email;

public class EmailPreferenceCreateTests
{
    [Fact]
    public void Create_WithDefaults_ReturnsEnabledPreference()
    {
        Guid userId = Guid.NewGuid();

        EmailPreference preference = EmailPreference.Create(userId, NotificationType.TaskAssigned);

        preference.UserId.Should().Be(userId);
        preference.NotificationType.Should().Be(NotificationType.TaskAssigned);
        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithExplicitDisabled_ReturnsDisabledPreference()
    {
        Guid userId = Guid.NewGuid();

        EmailPreference preference = EmailPreference.Create(userId, NotificationType.BillingInvoice, isEnabled: false);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_SetsCreatedAtTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.SystemNotification);

        preference.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        EmailPreference first = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);
        EmailPreference second = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);

        first.Id.Should().NotBe(second.Id);
    }
}

public class EmailPreferenceStateTests
{
    [Fact]
    public void Enable_SetsIsEnabledToTrue()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, isEnabled: false);

        preference.Enable(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_SetsUpdatedAtTimestamp()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, isEnabled: false);
        DateTime before = DateTime.UtcNow;

        preference.Enable(TimeProvider.System);

        preference.UpdatedAt.Should().NotBeNull();
        preference.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);

        preference.Disable(TimeProvider.System);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_SetsUpdatedAtTimestamp()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);
        DateTime before = DateTime.UtcNow;

        preference.Disable(TimeProvider.System);

        preference.UpdatedAt.Should().NotBeNull();
        preference.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Toggle_WhenEnabled_DisablesPreference()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);

        preference.Toggle(TimeProvider.System);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Toggle_WhenDisabled_EnablesPreference()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, isEnabled: false);

        preference.Toggle(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Toggle_SetsUpdatedAtTimestamp()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);
        DateTime before = DateTime.UtcNow;

        preference.Toggle(TimeProvider.System);

        preference.UpdatedAt.Should().NotBeNull();
        preference.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Toggle_CalledTwice_RestoresOriginalState()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);

        preference.Toggle(TimeProvider.System);
        preference.Toggle(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }
}
