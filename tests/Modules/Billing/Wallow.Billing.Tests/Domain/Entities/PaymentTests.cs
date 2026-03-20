using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Exceptions;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using static Wallow.Billing.Tests.Domain.Entities.PaymentTestHelpers;

namespace Wallow.Billing.Tests.Domain.Entities;

public class PaymentCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsPaymentInPendingStatus()
    {
        InvoiceId invoiceId = InvoiceId.New();
        Guid userId = Guid.NewGuid();
        Money amount = Money.Create(100, "USD");
        Guid createdBy = Guid.NewGuid();

        Payment payment = Payment.Create(invoiceId, userId, amount, PaymentMethod.CreditCard, createdBy, TimeProvider.System);

        payment.InvoiceId.Should().Be(invoiceId);
        payment.UserId.Should().Be(userId);
        payment.Amount.Should().Be(amount);
        payment.Method.Should().Be(PaymentMethod.CreditCard);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.TransactionReference.Should().BeNull();
        payment.FailureReason.Should().BeNull();
        payment.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesPaymentCreatedDomainEvent()
    {
        InvoiceId invoiceId = InvoiceId.New();
        Guid userId = Guid.NewGuid();
        Money amount = Money.Create(250, "EUR");

        Payment payment = Payment.Create(invoiceId, userId, amount, PaymentMethod.BankTransfer, Guid.NewGuid(), TimeProvider.System);

        payment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentCreatedDomainEvent>()
            .Which.Should().Match<PaymentCreatedDomainEvent>(e =>
                e.InvoiceId == invoiceId.Value &&
                e.UserId == userId &&
                e.Amount == 250 &&
                e.Currency == "EUR");
    }

    [Fact]
    public void Create_WithZeroAmount_ThrowsInvalidPaymentException()
    {
        Func<Payment> act = () => Payment.Create(InvoiceId.New(), Guid.NewGuid(), Money.Create(0, "USD"), PaymentMethod.CreditCard, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void Create_WithCustomFields_SetsCustomFields()
    {
        Dictionary<string, object> customFields = new Dictionary<string, object>
        {
            { "gateway", "stripe" },
            { "reference", "pay_123" }
        };

        Payment payment = Payment.Create(InvoiceId.New(), Guid.NewGuid(), Money.Create(100, "USD"), PaymentMethod.CreditCard, Guid.NewGuid(), TimeProvider.System, customFields);

        payment.CustomFields.Should().NotBeNull();
        payment.CustomFields.Should().ContainKey("gateway");
        payment.CustomFields!["gateway"].Should().Be("stripe");
    }
}

public class PaymentCompleteTests
{
    [Fact]
    public void Complete_PendingPayment_ChangesStatusToCompleted()
    {
        Payment payment = CreatePendingPayment();
        string transactionRef = "txn_abc123";
        Guid updatedBy = Guid.NewGuid();
        DateTime beforeComplete = DateTime.UtcNow;

        payment.Complete(transactionRef, updatedBy, TimeProvider.System);

        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionReference.Should().Be(transactionRef);
        payment.CompletedAt.Should().NotBeNull();
        payment.CompletedAt.Should().BeOnOrAfter(beforeComplete);
    }

    [Fact]
    public void Complete_CompletedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Complete("txn_2", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Complete_FailedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Fail("Card declined", Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Complete_RefundedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);
        payment.Refund(Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Complete("txn_2", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Pending*");
    }
}

public class PaymentFailTests
{
    [Fact]
    public void Fail_PendingPayment_ChangesStatusToFailed()
    {
        Payment payment = CreatePendingPayment();
        string reason = "Insufficient funds";
        Guid updatedBy = Guid.NewGuid();

        payment.Fail(reason, updatedBy, TimeProvider.System);

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be(reason);
    }

    [Fact]
    public void Fail_RaisesPaymentFailedDomainEvent()
    {
        Payment payment = CreatePendingPayment();
        string reason = "Card expired";
        payment.ClearDomainEvents();

        payment.Fail(reason, Guid.NewGuid(), TimeProvider.System);

        payment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentFailedDomainEvent>()
            .Which.Should().Match<PaymentFailedDomainEvent>(e =>
                e.PaymentId == payment.Id.Value &&
                e.InvoiceId == payment.InvoiceId.Value &&
                e.Reason == reason &&
                e.UserId == payment.UserId);
    }

    [Fact]
    public void Fail_CompletedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Fail("Some reason", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Fail_FailedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Fail("First failure", Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Fail("Second failure", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Pending*");
    }
}

public class PaymentRefundTests
{
    [Fact]
    public void Refund_CompletedPayment_ChangesStatusToRefunded()
    {
        Payment payment = CreatePendingPayment();
        payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);
        Guid updatedBy = Guid.NewGuid();

        payment.Refund(updatedBy, TimeProvider.System);

        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refund_PendingPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();

        Action act = () => payment.Refund(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Completed*");
    }

    [Fact]
    public void Refund_FailedPayment_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Fail("Card declined", Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Refund(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Completed*");
    }

    [Fact]
    public void Refund_AlreadyRefunded_ThrowsInvalidPaymentException()
    {
        Payment payment = CreatePendingPayment();
        payment.Complete("txn_1", Guid.NewGuid(), TimeProvider.System);
        payment.Refund(Guid.NewGuid(), TimeProvider.System);

        Action act = () => payment.Refund(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("*Completed*");
    }
}

internal static class PaymentTestHelpers
{
    public static Payment CreatePendingPayment()
    {
        Payment payment = Payment.Create(InvoiceId.New(), Guid.NewGuid(), Money.Create(100, "USD"), PaymentMethod.CreditCard, Guid.NewGuid(), TimeProvider.System);
        payment.ClearDomainEvents();
        return payment;
    }
}
