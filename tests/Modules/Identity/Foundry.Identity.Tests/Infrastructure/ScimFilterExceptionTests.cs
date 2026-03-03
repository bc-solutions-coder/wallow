using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimFilterExceptionTests
{
    [Fact]
    public void Constructor_WithMessageAndPosition_FormatsMessage()
    {
        ScimFilterException ex = new ScimFilterException("Unexpected token", 5);

        ex.Message.Should().Be("Unexpected token at position 5");
        ex.Position.Should().Be(5);
    }

    [Fact]
    public void Constructor_WithMessageAndNegativePosition_UsesMessageOnly()
    {
        ScimFilterException ex = new ScimFilterException("General error", -1);

        ex.Message.Should().Be("General error");
        ex.Position.Should().Be(-1);
    }

    [Fact]
    public void Constructor_WithMessagePositionAndInnerException_FormatsMessage()
    {
        Exception inner = new InvalidOperationException("Inner");
        ScimFilterException ex = new ScimFilterException("Parse error", 10, inner);

        ex.Message.Should().Be("Parse error at position 10");
        ex.Position.Should().Be(10);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Constructor_WithMessagePositionAndInnerException_NegativePosition_UsesMessageOnly()
    {
        Exception inner = new InvalidOperationException("Inner");
        ScimFilterException ex = new ScimFilterException("Generic", -1, inner);

        ex.Message.Should().Be("Generic");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Constructor_Default_HasDefaultMessage()
    {
        ScimFilterException ex = new ScimFilterException();

        ex.Message.Should().NotBeNullOrEmpty();
        ex.Position.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithMessageOnly_SetsMessage()
    {
        ScimFilterException ex = new ScimFilterException("Simple error");

        ex.Message.Should().Be("Simple error");
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsProperties()
    {
        InvalidOperationException inner = new InvalidOperationException("Inner");
        ScimFilterException ex = new ScimFilterException("Outer error", inner);

        ex.Message.Should().Be("Outer error");
        ex.InnerException.Should().Be(inner);
    }
}
