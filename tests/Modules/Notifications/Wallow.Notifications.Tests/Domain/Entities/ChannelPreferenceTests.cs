using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Notifications.Domain.Preferences.Events;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class ChannelPreferenceTests
{
    [Fact]
    public void Create_WithValidData_SetsPropertiesAndRaisesEvent()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference preference = ChannelPreference.Create(
            userId, ChannelType.Email, "*", TimeProvider.System, isEnabled: true);

        preference.UserId.Should().Be(userId);
        preference.ChannelType.Should().Be(ChannelType.Email);
        preference.NotificationType.Should().Be("*");
        preference.IsEnabled.Should().BeTrue();
        preference.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ChannelPreferenceCreatedEvent>();
    }

    [Fact]
    public void Create_DisabledByDefault_SetsIsEnabledFalse()
    {
        ChannelPreference preference = ChannelPreference.Create(
            Guid.NewGuid(), ChannelType.Push, "Alert", TimeProvider.System, isEnabled: false);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyNotificationType_ThrowsArgumentException()
    {
        Action act = () => ChannelPreference.Create(
            Guid.NewGuid(), ChannelType.Email, string.Empty, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enable_SetsIsEnabledTrue()
    {
        ChannelPreference preference = ChannelPreference.Create(
            Guid.NewGuid(), ChannelType.Email, "*", TimeProvider.System, isEnabled: false);

        preference.Enable(TimeProvider.System);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        ChannelPreference preference = ChannelPreference.Create(
            Guid.NewGuid(), ChannelType.Sms, "*", TimeProvider.System, isEnabled: true);

        preference.Disable(TimeProvider.System);

        preference.IsEnabled.Should().BeFalse();
    }
}
