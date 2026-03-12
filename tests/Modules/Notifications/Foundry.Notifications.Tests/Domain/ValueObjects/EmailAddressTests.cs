using Foundry.Notifications.Domain.Channels.Email.Exceptions;
using Foundry.Notifications.Domain.Channels.Email.ValueObjects;

namespace Foundry.Notifications.Tests.Domain.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.name@domain.co")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("name+tag@company.org")]
    public void Create_WithValidEmail_ReturnsEmailAddress(string email)
    {
        EmailAddress result = EmailAddress.Create(email);

        result.Value.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public void Create_NormalizesToLowercase()
    {
        EmailAddress result = EmailAddress.Create("John.Doe@Example.COM");

        result.Value.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        EmailAddress result = EmailAddress.Create("  user@example.com  ");

        result.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNull_ThrowsInvalidEmailAddressException(string? email)
    {
        Action act = () => EmailAddress.Create(email!);

        act.Should().Throw<InvalidEmailAddressException>();
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@tld")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    public void Create_WithInvalidFormat_ThrowsInvalidEmailAddressException(string email)
    {
        Action act = () => EmailAddress.Create(email);

        act.Should().Throw<InvalidEmailAddressException>();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        EmailAddress email = EmailAddress.Create("test@example.com");

        email.ToString().Should().Be("test@example.com");
    }

    [Fact]
    public void ImplicitConversion_ReturnsStringValue()
    {
        EmailAddress email = EmailAddress.Create("test@example.com");

        string result = email;

        result.Should().Be("test@example.com");
    }

    [Fact]
    public void Equals_WithSameEmail_ReturnsTrue()
    {
        EmailAddress first = EmailAddress.Create("test@example.com");
        EmailAddress second = EmailAddress.Create("TEST@EXAMPLE.COM");

        first.Should().Be(second);
    }

    [Fact]
    public void Equals_WithDifferentEmail_ReturnsFalse()
    {
        EmailAddress first = EmailAddress.Create("one@example.com");
        EmailAddress second = EmailAddress.Create("two@example.com");

        first.Should().NotBe(second);
    }
}
