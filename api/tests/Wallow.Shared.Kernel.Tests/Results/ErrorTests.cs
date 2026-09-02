using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Shared.Kernel.Tests.Results;

public class ErrorTests
{
    private static readonly ErrorCatalogEntry _entry = new("Test.Something", ErrorKind.Conflict, "Something conflicts.");

    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_FromEntry_CopiesCodeKindAndDefaultMessage()
    {
        Error error = new(_entry);

        error.Code.Should().Be("Test.Something");
        error.Kind.Should().Be(ErrorKind.Conflict);
        error.Message.Should().Be("Something conflicts.");
    }

    [Fact]
    public void Constructor_WithOverride_KeepsCodeAndKindButReplacesMessage()
    {
        Error error = new(_entry, "This one conflicts.");

        error.Code.Should().Be("Test.Something");
        error.Kind.Should().Be(ErrorKind.Conflict);
        error.Message.Should().Be("This one conflicts.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankOverride_Throws(string message)
    {
        Func<Error> act = () => new Error(_entry, message);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullEntry_Throws()
    {
        Func<Error> act = () => new Error(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_IsStructural()
    {
        Error first = new(_entry, "same");
        Error second = new(_entry, "same");

        first.Should().Be(second);
        first.Should().NotBe(new Error(_entry, "different"));
        first.Should().NotBe(Error.None);
    }
}
