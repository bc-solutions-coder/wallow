using Wallow.Notifications.Domain.Channels.Email.ValueObjects;

namespace Wallow.Notifications.Tests.Domain.ValueObjects;

public class EmailContentTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        EmailContent content = EmailContent.Create("Welcome", "Hello, World!");

        content.Subject.Should().Be("Welcome");
        content.Body.Should().Be("Hello, World!");
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        EmailContent content = EmailContent.Create("  Subject  ", "  Body  ");

        content.Subject.Should().Be("Subject");
        content.Body.Should().Be("Body");
    }

    [Fact]
    public void Create_WithEmptySubject_ThrowsArgumentException()
    {
        Action act = () => EmailContent.Create(string.Empty, "Body");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyBody_ThrowsArgumentException()
    {
        Action act = () => EmailContent.Create("Subject", string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceSubject_ThrowsArgumentException()
    {
        Action act = () => EmailContent.Create("   ", "Body");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameContent_AreEqual()
    {
        EmailContent content1 = EmailContent.Create("Subject", "Body");
        EmailContent content2 = EmailContent.Create("Subject", "Body");

        content1.Should().Be(content2);
    }

    [Fact]
    public void Equality_DifferentSubject_AreNotEqual()
    {
        EmailContent content1 = EmailContent.Create("Subject A", "Body");
        EmailContent content2 = EmailContent.Create("Subject B", "Body");

        content1.Should().NotBe(content2);
    }
}
