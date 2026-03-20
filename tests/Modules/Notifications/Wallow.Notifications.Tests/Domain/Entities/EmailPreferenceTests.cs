using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class EmailPreferenceTests
{
    [Fact]
    public void Create_WithValidData_SetsPropertiesCorrectly()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.BillingInvoice, true);

        preference.UserId.Should().Be(userId);
        preference.NotificationType.Should().Be(NotificationType.BillingInvoice);
        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultIsEnabled_ReturnsTrue()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_SetsIsEnabledTrue()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.Mention, false);

        preference.Enable(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.BillingInvoice, true);

        preference.Disable(TimeProvider.System);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Toggle_WhenEnabled_DisablesPreference()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, true);

        preference.Toggle(TimeProvider.System);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Toggle_WhenDisabled_EnablesPreference()
    {
        EmailPreference preference = EmailPreference.Create(Guid.NewGuid(), NotificationType.TaskAssigned, false);

        preference.Toggle(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }
}
