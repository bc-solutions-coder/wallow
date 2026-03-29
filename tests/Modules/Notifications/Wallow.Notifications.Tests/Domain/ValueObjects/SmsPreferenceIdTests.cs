using Wallow.Notifications.Domain.Channels.Sms.Identity;

namespace Wallow.Notifications.Tests.Domain.ValueObjects;

public class SmsPreferenceIdTests
{
    [Fact]
    public void New_ReturnsTwoUniqueIds()
    {
        SmsPreferenceId first = SmsPreferenceId.New();
        SmsPreferenceId second = SmsPreferenceId.New();

        first.Should().NotBe(second);
        first.Value.Should().NotBe(Guid.Empty);
        second.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithGuid_RoundTrips()
    {
        Guid guid = Guid.NewGuid();

        SmsPreferenceId id = SmsPreferenceId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Create_WithEmptyGuid_ReturnsIdWithEmptyValue()
    {
        SmsPreferenceId id = SmsPreferenceId.Create(Guid.Empty);

        id.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Equality_SameGuid_AreEqual()
    {
        Guid guid = Guid.NewGuid();

        SmsPreferenceId first = SmsPreferenceId.Create(guid);
        SmsPreferenceId second = SmsPreferenceId.Create(guid);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentGuids_AreNotEqual()
    {
        SmsPreferenceId first = SmsPreferenceId.New();
        SmsPreferenceId second = SmsPreferenceId.New();

        first.Should().NotBe(second);
        (first != second).Should().BeTrue();
    }
}
