using Wallow.Shared.Kernel.Domain;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class ExternalServiceExceptionTests
{
    [Fact]
    public void Constructor_WithStatusCodeAndResponseBody_SetsProperties()
    {
        ExternalServiceException ex = new("Service failed", 503, "{ \"error\": \"timeout\" }");

        ex.Message.Should().Be("Service failed");
        ex.StatusCode.Should().Be(503);
        ex.ResponseBody.Should().Be("{ \"error\": \"timeout\" }");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithStatusCodeResponseBodyAndInnerException_SetsAll()
    {
        InvalidOperationException inner = new("root cause");

        ExternalServiceException ex = new("Service failed", 500, "error body", inner);

        ex.Message.Should().Be("Service failed");
        ex.StatusCode.Should().Be(500);
        ex.ResponseBody.Should().Be("error body");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithStatusCodeAndNullResponseBody_SetsNullBody()
    {
        ExternalServiceException ex = new("Service failed", 404);

        ex.StatusCode.Should().Be(404);
        ex.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void Parameterless_Constructor_SetsDefaults()
    {
        ExternalServiceException ex = new();

        ex.StatusCode.Should().Be(0);
        ex.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        ExternalServiceException ex = new("something went wrong");

        ex.Message.Should().Be("something went wrong");
        ex.StatusCode.Should().Be(0);
        ex.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBoth()
    {
        InvalidOperationException inner = new("inner");

        ExternalServiceException ex = new("outer", inner);

        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
        ex.StatusCode.Should().Be(0);
        ex.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void InheritsFromException()
    {
        ExternalServiceException ex = new("test", 500);

        ex.Should().BeAssignableTo<Exception>();
    }
}
