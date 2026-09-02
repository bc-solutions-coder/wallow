using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class DomainExceptionTests
{
    private static readonly ErrorCatalogEntry _notFoundEntry = new("Invoice.NotFound", ErrorKind.NotFound, "Invoice not found.");
    private static readonly ErrorCatalogEntry _ruleEntry = new("Billing.InvoiceAlreadyPaid", ErrorKind.BusinessRule, "The invoice is already paid.");

    [Fact]
    public void Constructor_FromEntry_ExposesEntryCodeKindAndDefaultMessage()
    {
        TestDomainException exception = new(_ruleEntry);

        exception.Entry.Should().BeSameAs(_ruleEntry);
        exception.Code.Should().Be("Billing.InvoiceAlreadyPaid");
        exception.Kind.Should().Be(ErrorKind.BusinessRule);
        exception.Message.Should().Be("The invoice is already paid.");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOverride_ReplacesMessageOnly()
    {
        TestDomainException exception = new(_ruleEntry, "Invoice 42 is already paid.");

        exception.Code.Should().Be("Billing.InvoiceAlreadyPaid");
        exception.Message.Should().Be("Invoice 42 is already paid.");
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesIt()
    {
        InvalidOperationException inner = new("boom");

        TestDomainException exception = new(_ruleEntry, null, inner);

        exception.InnerException.Should().BeSameAs(inner);
        exception.Message.Should().Be(_ruleEntry.DefaultMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankOverride_Throws(string message)
    {
        Func<TestDomainException> act = () => new TestDomainException(_ruleEntry, message);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullEntry_Throws()
    {
        Func<TestDomainException> act = () => new TestDomainException(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EntityNotFoundException_KeepsEntityIdAndEntry()
    {
        Guid id = Guid.NewGuid();

        EntityNotFoundException exception = new(_notFoundEntry, id);

        exception.EntityId.Should().Be(id);
        exception.Code.Should().Be("Invoice.NotFound");
        exception.Kind.Should().Be(ErrorKind.NotFound);
        exception.Message.Should().Be("Invoice not found.");
    }

    [Fact]
    public void EntityNotFoundException_RefusesNonNotFoundEntry()
    {
        Func<EntityNotFoundException> act = () => new EntityNotFoundException(_ruleEntry, 1);

        act.Should().Throw<ArgumentException>().WithMessage("*NotFound*");
    }

    [Fact]
    public void BusinessRuleException_CarriesEntryAndOverride()
    {
        BusinessRuleException exception = new(_ruleEntry, "Paid on Monday.");

        exception.Code.Should().Be("Billing.InvoiceAlreadyPaid");
        exception.Kind.Should().Be(ErrorKind.BusinessRule);
        exception.Message.Should().Be("Paid on Monday.");
    }

    [Fact]
    public void BusinessRuleException_RefusesNonBusinessRuleEntry()
    {
        Func<BusinessRuleException> act = () => new BusinessRuleException(_notFoundEntry);

        act.Should().Throw<ArgumentException>().WithMessage("*BusinessRule*");
    }

    private sealed class TestDomainException(ErrorCatalogEntry entry, string? message = null, Exception? inner = null)
        : DomainException(entry, message, inner);
}
