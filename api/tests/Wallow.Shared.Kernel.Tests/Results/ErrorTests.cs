using Wallow.Shared.Kernel.Results;

namespace Wallow.Shared.Kernel.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Constructor_WithCodeAndMessage_SetsProperties()
    {
        Error error = new("Test.Code", "Test message");

        error.Code.Should().Be("Test.Code");
        error.Message.Should().Be("Test message");
    }

    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void NullValue_HasExpectedCodeAndMessage()
    {
        Error.NullValue.Code.Should().Be("Error.NullValue");
        Error.NullValue.Message.Should().Be("A null value was provided");
    }

    [Fact]
    public void NotFound_CreatesErrorWithEntityInfo()
    {
        Error error = Error.NotFound("Invoice", 123);

        error.Code.Should().Be("Invoice.NotFound");
        error.Message.Should().Contain("Invoice").And.Contain("123");
    }

    [Fact]
    public void Validation_WithMessage_CreatesValidationError()
    {
        Error error = Error.Validation("Field is required");

        error.Code.Should().Be("Validation.Error");
        error.Message.Should().Be("Field is required");
    }

    [Fact]
    public void Validation_WithCodeAndMessage_CreatesCustomValidationError()
    {
        Error error = Error.Validation("Custom.Code", "Custom message");

        error.Code.Should().Be("Custom.Code");
        error.Message.Should().Be("Custom message");
    }

    [Fact]
    public void Conflict_CreatesConflictError()
    {
        Error error = Error.Conflict("Resource already exists");

        error.Code.Should().Be("Conflict.Error");
        error.Message.Should().Be("Resource already exists");
    }

    [Fact]
    public void Unauthorized_WithDefaultMessage_CreatesUnauthorizedError()
    {
        Error error = Error.Unauthorized();

        error.Code.Should().Be("Unauthorized.Error");
        error.Message.Should().Be("Unauthorized access");
    }

    [Fact]
    public void Unauthorized_WithCustomMessage_UsesCustomMessage()
    {
        Error error = Error.Unauthorized("Token expired");

        error.Code.Should().Be("Unauthorized.Error");
        error.Message.Should().Be("Token expired");
    }

    [Fact]
    public void Forbidden_WithDefaultMessage_CreatesForbiddenError()
    {
        Error error = Error.Forbidden();

        error.Code.Should().Be("Forbidden.Error");
        error.Message.Should().Be("Access denied");
    }

    [Fact]
    public void Forbidden_WithCustomMessage_UsesCustomMessage()
    {
        Error error = Error.Forbidden("Insufficient permissions");

        error.Code.Should().Be("Forbidden.Error");
        error.Message.Should().Be("Insufficient permissions");
    }

    [Fact]
    public void BusinessRule_CreatesErrorWithPrefixedCode()
    {
        Error error = Error.BusinessRule("InvoiceAlreadyPaid", "Cannot pay an already paid invoice");

        error.Code.Should().Be("BusinessRule.InvoiceAlreadyPaid");
        error.Message.Should().Be("Cannot pay an already paid invoice");
    }

    [Fact]
    public void Equality_SameCodeAndMessage_AreEqual()
    {
        Error error1 = new("Test.Code", "Test message");
        Error error2 = new("Test.Code", "Test message");

        error1.Should().Be(error2);
    }

    [Fact]
    public void Equality_DifferentCode_AreNotEqual()
    {
        Error error1 = new("Code.A", "Same message");
        Error error2 = new("Code.B", "Same message");

        error1.Should().NotBe(error2);
    }

    [Fact]
    public void Equality_DifferentMessage_AreNotEqual()
    {
        Error error1 = new("Same.Code", "Message A");
        Error error2 = new("Same.Code", "Message B");

        error1.Should().NotBe(error2);
    }
}
