using Foundry.Communications.Domain.Channels.Email.Exceptions;

namespace Foundry.Communications.Tests.Domain.Email;

public class InvalidEmailAddressExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessageAndCode()
    {
        InvalidEmailAddressException exception = new("Test message");

        exception.Message.Should().Be("Test message");
        exception.Code.Should().Be("Email.InvalidEmailAddress");
    }
}
