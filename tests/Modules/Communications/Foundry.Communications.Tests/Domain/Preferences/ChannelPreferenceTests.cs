using Foundry.Communications.Domain.Preferences;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Communications.Domain.Preferences.Events;

namespace Foundry.Communications.Tests.Domain.Preferences;

public class ChannelPreferenceTests
{
    [Theory]
    [InlineData(ChannelType.Email)]
    [InlineData(ChannelType.Sms)]
    [InlineData(ChannelType.InApp)]
    [InlineData(ChannelType.Push)]
    [InlineData(ChannelType.Webhook)]
    public void Create_WithValidChannelType_SetsPropertiesCorrectly(ChannelType channelType)
    {
        Guid userId = Guid.NewGuid();

        ChannelPreference preference = ChannelPreference.Create(
            userId,
            channelType,
            NotificationTypes.TaskAssigned);

        preference.UserId.Should().Be(userId);
        preference.ChannelType.Should().Be(channelType);
        preference.NotificationType.Should().Be(NotificationTypes.TaskAssigned);
        preference.Id.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithDefaultIsEnabled_DefaultsToTrue()
    {
        ChannelPreference preference = ChannelPreference.Create(
            Guid.NewGuid(),
            ChannelType.Email,
            NotificationTypes.BillingInvoice);

        preference.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithIsEnabledFalse_SetsIsEnabledToFalse()
    {
        ChannelPreference preference = ChannelPreference.Create(
            Guid.NewGuid(),
            ChannelType.Email,
            NotificationTypes.SystemAlert,
            isEnabled: false);

        preference.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_RaisesChannelPreferenceCreatedEvent()
    {
        Guid userId = Guid.NewGuid();

        ChannelPreference preference = ChannelPreference.Create(
            userId,
            ChannelType.InApp,
            NotificationTypes.UserMention);

        preference.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ChannelPreferenceCreatedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                ChannelPreferenceId = preference.Id,
                UserId = userId,
                ChannelType = ChannelType.InApp,
                NotificationType = NotificationTypes.UserMention,
                IsEnabled = true
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithNullOrEmptyNotificationType_ThrowsArgumentException(string? notificationType)
    {
        Action act = () => ChannelPreference.Create(
            Guid.NewGuid(),
            ChannelType.Email,
            notificationType!);

        act.Should().Throw<ArgumentException>();
    }
}
