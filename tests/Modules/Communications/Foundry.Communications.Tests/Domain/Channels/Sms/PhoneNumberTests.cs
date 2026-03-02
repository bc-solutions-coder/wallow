using Foundry.Communications.Domain.Channels.Sms.Exceptions;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;

namespace Foundry.Communications.Tests.Domain.Channels.Sms;

public class PhoneNumberCreateTests
{
    [Theory]
    [InlineData("+14155551234")]
    [InlineData("+442071234567")]
    [InlineData("+81312345678")]
    [InlineData("+1234567890")]
    public void Create_WithValidE164Number_ReturnsPhoneNumber(string number)
    {
        PhoneNumber phone = PhoneNumber.Create(number);

        phone.Value.Should().Be(number);
    }

    [Fact]
    public void Create_WithLeadingTrailingSpaces_TrimsInput()
    {
        PhoneNumber phone = PhoneNumber.Create("  +14155551234  ");

        phone.Value.Should().Be("+14155551234");
    }

    [Theory]
    [InlineData("14155551234")]
    [InlineData("4155551234")]
    public void Create_WithoutPlusPrefix_ThrowsInvalidPhoneNumberException(string number)
    {
        Action act = () => PhoneNumber.Create(number);

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Theory]
    [InlineData("+12345")]
    [InlineData("+123456")]
    public void Create_WithTooShortNumber_ThrowsInvalidPhoneNumberException(string number)
    {
        Action act = () => PhoneNumber.Create(number);

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Fact]
    public void Create_WithTooLongNumber_ThrowsInvalidPhoneNumberException()
    {
        Action act = () => PhoneNumber.Create("+1234567890123456");

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Theory]
    [InlineData("+1415555abcd")]
    [InlineData("+1abc5551234")]
    public void Create_WithLetters_ThrowsInvalidPhoneNumberException(string number)
    {
        Action act = () => PhoneNumber.Create(number);

        act.Should().Throw<InvalidPhoneNumberException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ThrowsInvalidPhoneNumberException(string? number)
    {
        Action act = () => PhoneNumber.Create(number!);

        act.Should().Throw<InvalidPhoneNumberException>();
    }
}

public class PhoneNumberConversionTests
{
    [Fact]
    public void ToString_ReturnsPhoneValue()
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
}
