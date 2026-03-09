using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Exceptions;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Domain;
using Foundry.Tests.Common.Builders;

namespace Foundry.Billing.Tests.Domain.Entities;

public class InvoiceCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInvoiceInDraftStatus()
    {
        Guid userId = Guid.NewGuid();
        Guid createdBy = Guid.NewGuid();

        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", createdBy, TimeProvider.System);

        invoice.UserId.Should().Be(userId);
        invoice.InvoiceNumber.Should().Be("INV-001");
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.TotalAmount.Amount.Should().Be(0);
        invoice.TotalAmount.Currency.Should().Be("USD");
        invoice.LineItems.Should().BeEmpty();
        invoice.DueDate.Should().BeNull();
        invoice.PaidAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithDueDate_SetsDueDate()
    {
        DateTime dueDate = DateTime.UtcNow.AddDays(30);

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, dueDate);

        invoice.DueDate.Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_RaisesInvoiceCreatedDomainEvent()
    {
        Guid userId = Guid.NewGuid();

        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", Guid.NewGuid(), TimeProvider.System);

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCreatedDomainEvent>()
            .Which.UserId.Should().Be(userId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyInvoiceNumber_ThrowsBusinessRuleException(string? invoiceNumber)
    {
        Func<Invoice> act = () => Invoice.Create(Guid.NewGuid(), invoiceNumber!, "USD", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.InvoiceNumberRequired");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsBusinessRuleException()
    {
        Func<Invoice> act = () => Invoice.Create(Guid.Empty, "INV-001", "USD", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.UserIdRequired");
    }
}

public class InvoiceLineItemTests
{
    [Fact]
    public void AddLineItem_ToDraftInvoice_AddsItemAndUpdatesTotal()
    {
        Invoice invoice = InvoiceBuilder.Create().Build();

        invoice.AddLineItem("Consulting", Money.Create(500, "USD"), 2, Guid.NewGuid(), TimeProvider.System);

        invoice.LineItems.Should().ContainSingle();
        invoice.TotalAmount.Amount.Should().Be(1000); // 500 * 2
    }

    [Fact]
    public void AddLineItem_MultipleItems_AccumulatesTotal()
    {
        Invoice invoice = InvoiceBuilder.Create().Build();

        invoice.AddLineItem("Service A", Money.Create(100, "USD"), 1, Guid.NewGuid(), TimeProvider.System);
        invoice.AddLineItem("Service B", Money.Create(200, "USD"), 2, Guid.NewGuid(), TimeProvider.System);

        invoice.LineItems.Should().HaveCount(2);
        invoice.TotalAmount.Amount.Should().Be(500); // 100 + (200 * 2)
    }

    [Fact]
    public void AddLineItem_ToIssuedInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();

        Action act = () => invoice.AddLineItem("Extra", Money.Create(100, "USD"), 1, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*draft*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void AddLineItem_WithInvalidQuantity_ThrowsBusinessRuleException(int quantity)
    {
        Invoice invoice = InvoiceBuilder.Create().Build();

        Action act = () => invoice.AddLineItem("Item", Money.Create(100, "USD"), quantity, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.InvalidQuantity");
    }

    [Fact]
    public void RemoveLineItem_FromDraftInvoice_RemovesItemAndUpdatesTotal()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithLineItem("Item 1", 100)
            .WithLineItem("Item 2", 200)
            .Build();
        InvoiceLineItem itemToRemove = invoice.LineItems.First();

        invoice.RemoveLineItem(itemToRemove.Id, Guid.NewGuid(), TimeProvider.System);

        invoice.LineItems.Should().ContainSingle();
        invoice.TotalAmount.Amount.Should().Be(200);
    }

    [Fact]
    public void RemoveLineItem_FromIssuedInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();
        InvoiceLineItemId itemId = invoice.LineItems.First().Id;

        Action act = () => invoice.RemoveLineItem(itemId, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*draft*");
    }

    [Fact]
    public void RemoveLineItem_NonExistentItem_ThrowsBusinessRuleException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .Build();

        Action act = () => invoice.RemoveLineItem(InvoiceLineItemId.New(), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.LineItemNotFound");
    }
}

public class InvoiceIssueTests
{
    [Fact]
    public void Issue_DraftWithLineItems_ChangesStatusToIssued()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithLineItem("Service", 500)
            .Build();

        invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public void Issue_DraftWithoutLineItems_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create().Build();

        Action act = () => invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*no line items*");
    }

    [Fact]
    public void Issue_AlreadyIssued_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();

        Action act = () => invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*draft*");
    }

    [Fact]
    public void Issue_PaidInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsPaid()
            .Build();

        Action act = () => invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>();
    }
}

public class InvoiceMarkAsPaidTests
{
    [Fact]
    public void MarkAsPaid_IssuedInvoice_ChangesStatusAndSetsPaidAt()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();
        Guid paymentId = Guid.NewGuid();
        DateTime beforePaid = DateTime.UtcNow;

        invoice.MarkAsPaid(paymentId, Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().NotBeNull();
        invoice.PaidAt.Should().BeOnOrAfter(beforePaid);
    }

    [Fact]
    public void MarkAsPaid_OverdueInvoice_ChangesStatusToPaid()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsOverdue()
            .Build();

        invoice.MarkAsPaid(Guid.NewGuid(), Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_RaisesInvoicePaidDomainEvent()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();
        Guid paymentId = Guid.NewGuid();

        invoice.MarkAsPaid(paymentId, Guid.NewGuid(), TimeProvider.System);

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoicePaidDomainEvent>()
            .Which.PaymentId.Should().Be(paymentId);
    }

    [Fact]
    public void MarkAsPaid_DraftInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .Build();

        Action act = () => invoice.MarkAsPaid(Guid.NewGuid(), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*issued or overdue*");
    }

    [Fact]
    public void MarkAsPaid_AlreadyPaid_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsPaid()
            .Build();

        Action act = () => invoice.MarkAsPaid(Guid.NewGuid(), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>();
    }
}

public class InvoiceMarkAsOverdueTests
{
    [Fact]
    public void MarkAsOverdue_IssuedInvoice_ChangesStatusToOverdue()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();

        invoice.MarkAsOverdue(Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
    }

    [Fact]
    public void MarkAsOverdue_RaisesInvoiceOverdueDomainEvent()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();

        invoice.MarkAsOverdue(Guid.NewGuid(), TimeProvider.System);

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceOverdueDomainEvent>();
    }

    [Fact]
    public void MarkAsOverdue_DraftInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .Build();

        Action act = () => invoice.MarkAsOverdue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*issued*");
    }

    [Fact]
    public void MarkAsOverdue_PaidInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsPaid()
            .Build();

        Action act = () => invoice.MarkAsOverdue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>();
    }
}

public class InvoiceCancelTests
{
    [Fact]
    public void Cancel_DraftInvoice_ChangesStatusToCancelled()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .Build();

        invoice.Cancel(Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_IssuedInvoice_ChangesStatusToCancelled()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsIssued()
            .Build();

        invoice.Cancel(Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_OverdueInvoice_ChangesStatusToCancelled()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsOverdue()
            .Build();

        invoice.Cancel(Guid.NewGuid(), TimeProvider.System);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_PaidInvoice_ThrowsInvalidInvoiceException()
    {
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsPaid()
            .Build();

        Action act = () => invoice.Cancel(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidInvoiceException>()
            .WithMessage("*paid*");
    }
}
