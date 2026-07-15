using Wallow.Shared.Kernel.Domain;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class ForbiddenAccessExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        ForbiddenAccessException ex = new("you shall not pass");

        ex.Message.Should().Be("you shall not pass");
    }

    [Fact]
    public void Code_IsAlwaysAccessForbidden()
    {
        ForbiddenAccessException ex = new("any message");

        ex.Code.Should().Be("Access.Forbidden");
    }

    [Fact]
    public void InheritsFromDomainException()
    {
        ForbiddenAccessException ex = new("forbidden");

        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void InheritsFromException()
    {
        ForbiddenAccessException ex = new("forbidden");

        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void TwoInstances_WithDifferentMessages_CarryRespectiveMessagesButSameCode()
    {
        ForbiddenAccessException first = new("not allowed to read");
        ForbiddenAccessException second = new("not allowed to write");

        first.Message.Should().Be("not allowed to read");
        second.Message.Should().Be("not allowed to write");
        first.Code.Should().Be(second.Code).And.Be("Access.Forbidden");
    }
}
