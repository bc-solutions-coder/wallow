using Foundry.Inquiries.Domain.Exceptions;

namespace Foundry.Inquiries.Tests.Domain.Exceptions;

public class InvalidInquiryStatusTransitionExceptionTests
{
    [Fact]
    public void Constructor_SetsCorrectCodeAndMessage()
    {
        string from = "New";
        string to = "Closed";

        InvalidInquiryStatusTransitionException exception = new(from, to);

        exception.Code.Should().Be("Inquiries.InvalidStatusTransition");
        exception.Message.Should().Contain(from).And.Contain(to);
    }
}
