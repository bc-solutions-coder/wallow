using Foundry.Configuration.Domain.Exceptions;

namespace Foundry.Configuration.Tests.Domain;

public class CustomFieldExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessageAndCode()
    {
        CustomFieldException exception = new CustomFieldException("Something went wrong");

        exception.Message.Should().Be("Something went wrong");
        exception.Code.Should().Be("Configuration.CustomField");
    }
}
