using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.ValueObjects;

namespace Wallow.Notifications.Tests.Domain.Entities;

public class SmsPreferenceTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        Guid userId = Guid.NewGuid();
        PhoneNumber phone = PhoneNumber.Create("+12025550100");

        SmsPreference preference = SmsPreference.Create(userId, phone, true);

        preference.UserId.Should().Be(userId);
        preference.PhoneNumber.Value.Should().Be("+12025550100");
        preference.IsOptedIn.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultIsOptedIn_ReturnsTrue()
    {
        PhoneNumber phone = PhoneNumber.Create("+447911123456");
        SmsPreference preference = SmsPreference.Create(Guid.NewGuid(), phone);

        preference.IsOptedIn.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenOptedOut_SetsIsOptedInFalse()
    {
        PhoneNumber phone = PhoneNumber.Create("+12025550100");
        SmsPreference preference = SmsPreference.Create(Guid.NewGuid(), phone, false);

        preference.IsOptedIn.Should().BeFalse();
    }
}
