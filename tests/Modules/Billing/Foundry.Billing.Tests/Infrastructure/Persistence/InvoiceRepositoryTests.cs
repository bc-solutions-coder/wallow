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
public class InvoiceRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private InvoiceRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsInvoice()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice = Invoice.Create(TestUserId, "INV-REPO-001", "USD", TestUserId, TimeProvider.System);

        repository.Add(invoice);
        await repository.SaveChangesAsync();

        Invoice? result = await repository.GetByIdAsync(invoice.Id);

        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV-REPO-001");
    }

    [Fact]
    public async Task GetByIdWithLineItemsAsync_ReturnsInvoiceWithLineItems()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice = Invoice.Create(TestUserId, "INV-REPO-002", "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Test Service", Money.Create(100m, "USD"), 2, TestUserId, TimeProvider.System);

        repository.Add(invoice);
        await repository.SaveChangesAsync();

        Invoice? result = await repository.GetByIdWithLineItemsAsync(invoice.Id);

        result.Should().NotBeNull();
        result.LineItems.Should().HaveCount(1);
        result.LineItems.First().Description.Should().Be("Test Service");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsInvoicesForUser()
    {
        InvoiceRepository repository = CreateRepository();
        Guid userId = Guid.NewGuid();
        Invoice invoice1 = Invoice.Create(userId, "INV-REPO-003", "USD", TestUserId, TimeProvider.System);
        Invoice invoice2 = Invoice.Create(userId, "INV-REPO-004", "USD", TestUserId, TimeProvider.System);
        Invoice otherInvoice = Invoice.Create(Guid.NewGuid(), "INV-REPO-005", "USD", TestUserId, TimeProvider.System);

        repository.Add(invoice1);
        repository.Add(invoice2);
        repository.Add(otherInvoice);
        await repository.SaveChangesAsync();

        IReadOnlyList<Invoice> result = await repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInvoicesForTenant()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice1 = Invoice.Create(TestUserId, "INV-REPO-006", "USD", TestUserId, TimeProvider.System);
        Invoice invoice2 = Invoice.Create(TestUserId, "INV-REPO-007", "EUR", TestUserId, TimeProvider.System);

        repository.Add(invoice1);
        repository.Add(invoice2);
        await repository.SaveChangesAsync();

        IReadOnlyList<Invoice> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExistsByInvoiceNumberAsync_WhenExists_ReturnsTrue()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice = Invoice.Create(TestUserId, "INV-REPO-008", "USD", TestUserId, TimeProvider.System);
        repository.Add(invoice);
        await repository.SaveChangesAsync();

        bool exists = await repository.ExistsByInvoiceNumberAsync("INV-REPO-008");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByInvoiceNumberAsync_WhenNotExists_ReturnsFalse()
    {
        InvoiceRepository repository = CreateRepository();

        bool exists = await repository.ExistsByInvoiceNumberAsync("INV-NONEXISTENT");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ModifiesExistingInvoice()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice = Invoice.Create(TestUserId, "INV-REPO-009", "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Item", Money.Create(100m, "USD"), 1, TestUserId, TimeProvider.System);
        repository.Add(invoice);
        await repository.SaveChangesAsync();

        invoice.Issue(TestUserId, TimeProvider.System);
        repository.Update(invoice);
        await repository.SaveChangesAsync();

        Invoice? result = await repository.GetByIdAsync(invoice.Id);
        result.Should().NotBeNull();
        result.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public async Task Remove_DeletesInvoice()
    {
        InvoiceRepository repository = CreateRepository();
        Invoice invoice = Invoice.Create(TestUserId, "INV-REPO-010", "USD", TestUserId, TimeProvider.System);
        repository.Add(invoice);
        await repository.SaveChangesAsync();

        repository.Remove(invoice);
        await repository.SaveChangesAsync();

        Invoice? result = await repository.GetByIdAsync(invoice.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        InvoiceRepository repository = CreateRepository();

        Invoice? result = await repository.GetByIdAsync(InvoiceId.New());

        result.Should().BeNull();
    }
}
