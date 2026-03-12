using Foundry.Notifications.Domain.Channels.Sms.Exceptions;
using Foundry.Notifications.Domain.Channels.Sms.ValueObjects;

namespace Foundry.Notifications.Tests.Domain.ValueObjects;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+14155551234")]
    [InlineData("+442071234567")]
    [InlineData("+61291234567")]
    [InlineData("+1234567")]
    public void Create_WithValidE164_ReturnsPhoneNumber(string number)
    {
        PhoneNumber result = PhoneNumber.Create(number);

        result.Value.Should().Be(number);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        PhoneNumber result = PhoneNumber.Create("  +14155551234  ");

        result.Value.Should().Be("+14155551234");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNull_ThrowsInvalidPhoneNumberException(string? number)
    {
        Action act = () => PhoneNumber.Create(number!);

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Theory]
    [InlineData("14155551234")]
    [InlineData("+0123456789")]
    [InlineData("+1")]
    [InlineData("not-a-number")]
    [InlineData("+1234567890123456")]
    public void Create_WithInvalidFormat_ThrowsInvalidPhoneNumberException(string number)
    {
        Action act = () => PhoneNumber.Create(number);

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        PhoneNumber phone = PhoneNumber.Create("+14155551234");

        phone.ToString().Should().Be("+14155551234");
    }

    [Fact]
    public void ImplicitConversion_ReturnsStringValue()
    {
        PhoneNumber phone = PhoneNumber.Create("+14155551234");

        string result = phone;

        result.Should().Be("+14155551234");
    }

    [Fact]
    public void Equals_WithSameNumber_ReturnsTrue()
    {
        PhoneNumber first = PhoneNumber.Create("+14155551234");
        PhoneNumber second = PhoneNumber.Create("+14155551234");

        first.Should().Be(second);
    }

    [Fact]
    public void Equals_WithDifferentNumber_ReturnsFalse()
    {
        PhoneNumber first = PhoneNumber.Create("+14155551234");
        PhoneNumber second = PhoneNumber.Create("+442071234567");

        first.Should().NotBe(second);
    }
}
