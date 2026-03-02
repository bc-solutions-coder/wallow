using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Billing.Infrastructure.Persistence.Repositories;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class PaymentRepositoryTests : DbContextIntegrationTestBase<BillingDbContext>
{
    public PaymentRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    private PaymentRepository CreateRepository() => new PaymentRepository(DbContext);

    private Invoice CreateIssuedInvoice(string invoiceNumber)
    {
        Invoice invoice = Invoice.Create(TestUserId, invoiceNumber, "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, TestUserId, TimeProvider.System);
        invoice.Issue(TestUserId, TimeProvider.System);
        return invoice;
    }

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsPayment()
    {
        PaymentRepository repository = CreateRepository();
        Invoice invoice = CreateIssuedInvoice("INV-PAY-001");
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Payment payment = Payment.Create(invoice.Id, TestUserId, Money.Create(100m, "USD"), PaymentMethod.CreditCard, TestUserId, TimeProvider.System);
        repository.Add(payment);
        await repository.SaveChangesAsync();

        Payment? result = await repository.GetByIdAsync(payment.Id);

        result.Should().NotBeNull();
        result!.Amount.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task GetByInvoiceIdAsync_ReturnsPaymentsForInvoice()
    {
        PaymentRepository repository = CreateRepository();
        Invoice invoice = CreateIssuedInvoice("INV-PAY-002");
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Payment payment1 = Payment.Create(invoice.Id, TestUserId, Money.Create(50m, "USD"), PaymentMethod.CreditCard, TestUserId, TimeProvider.System);
        Payment payment2 = Payment.Create(invoice.Id, TestUserId, Money.Create(50m, "USD"), PaymentMethod.BankTransfer, TestUserId, TimeProvider.System);
        repository.Add(payment1);
        repository.Add(payment2);
        await repository.SaveChangesAsync();

        IReadOnlyList<Payment> result = await repository.GetByInvoiceIdAsync(invoice.Id);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsPaymentsForUser()
    {
        PaymentRepository repository = CreateRepository();
        Invoice invoice = CreateIssuedInvoice("INV-PAY-003");
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Payment payment = Payment.Create(invoice.Id, TestUserId, Money.Create(100m, "USD"), PaymentMethod.CreditCard, TestUserId, TimeProvider.System);
        repository.Add(payment);
        await repository.SaveChangesAsync();

        IReadOnlyList<Payment> result = await repository.GetByUserIdAsync(TestUserId);

        result.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Should().AllSatisfy(p => p.UserId.Should().Be(TestUserId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPayments()
    {
        PaymentRepository repository = CreateRepository();
        Invoice invoice = CreateIssuedInvoice("INV-PAY-004");
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Payment payment = Payment.Create(invoice.Id, TestUserId, Money.Create(100m, "USD"), PaymentMethod.CreditCard, TestUserId, TimeProvider.System);
        repository.Add(payment);
        await repository.SaveChangesAsync();

        IReadOnlyList<Payment> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Update_ModifiesPayment()
    {
        PaymentRepository repository = CreateRepository();
        Invoice invoice = CreateIssuedInvoice("INV-PAY-005");
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Payment payment = Payment.Create(invoice.Id, TestUserId, Money.Create(100m, "USD"), PaymentMethod.CreditCard, TestUserId, TimeProvider.System);
        repository.Add(payment);
        await repository.SaveChangesAsync();

        payment.Complete("txn-123", TestUserId, TimeProvider.System);
        repository.Update(payment);
        await repository.SaveChangesAsync();

        Payment? result = await repository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        PaymentRepository repository = CreateRepository();

        Payment? result = await repository.GetByIdAsync(PaymentId.New());

        result.Should().BeNull();
    }
}
