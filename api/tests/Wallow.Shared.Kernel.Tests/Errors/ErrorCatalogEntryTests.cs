using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Errors;

public class ErrorCatalogEntryTests
{
    [Theory]
    [InlineData("Storage.QuotaExceeded")]
    [InlineData("Auth.Unauthenticated")]
    [InlineData("Mfa.CodeInvalid")]
    [InlineData("Http.NotFound")]
    [InlineData("Area1.Reason2")]
    public void Constructor_AcceptsDottedPascalCase(string code)
    {
        ErrorCatalogEntry entry = new(code, ErrorKind.Validation, "Sentence.");

        entry.Code.Should().Be(code);
        entry.Kind.Should().Be(ErrorKind.Validation);
        entry.DefaultMessage.Should().Be("Sentence.");
    }

    [Theory]
    [InlineData("invalid_credentials")]
    [InlineData("Storage")]
    [InlineData("storage.QuotaExceeded")]
    [InlineData("Storage.quotaExceeded")]
    [InlineData("Storage.Quota.Exceeded")]
    [InlineData("Storage.Quota-Exceeded")]
    [InlineData("Storage.")]
    [InlineData(".Exceeded")]
    [InlineData("Storage QuotaExceeded")]
    public void Constructor_RejectsAnythingElse(string code)
    {
        Func<ErrorCatalogEntry> act = () => new ErrorCatalogEntry(code, ErrorKind.Validation, "Sentence.");

        act.Should().Throw<ArgumentException>().WithMessage("*Area.Reason*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RejectsBlankMessage(string message)
    {
        Func<ErrorCatalogEntry> act = () => new ErrorCatalogEntry("Area.Reason", ErrorKind.Validation, message);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_IsStructural()
    {
        ErrorCatalogEntry first = new("Area.Reason", ErrorKind.Conflict, "Sentence.");
        ErrorCatalogEntry second = new("Area.Reason", ErrorKind.Conflict, "Sentence.");

        first.Should().Be(second);
    }
}
