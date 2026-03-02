using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Billing.Infrastructure.Persistence.Repositories;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class InvoiceRepositoryExtensionsTests : DbContextIntegrationTestBase<BillingDbContext>
{
    public InvoiceRepositoryExtensionsTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    [Fact]
    public async Task FindByCustomFieldAsync_ReturnsMatchingInvoices()
    {
        Invoice invoice = Invoice.Create(TestUserId, $"INV-CF-{Guid.NewGuid():N}", "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, TestUserId, TimeProvider.System);
        invoice.SetCustomFields(new Dictionary<string, object>
        {
            ["department"] = "engineering",
            ["project"] = "alpha"
        });

        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<Invoice> result = await DbContext.Invoices.FindByCustomFieldAsync("department", "engineering");

        result.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Should().Contain(i => i.Id == invoice.Id);
    }

    [Fact]
    public async Task FindByCustomFieldAsync_WithNoMatch_ReturnsEmpty()
    {
        IReadOnlyList<Invoice> result = await DbContext.Invoices.FindByCustomFieldAsync("nonexistent", "value");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByCustomFieldsAsync_ReturnsMatchingInvoices()
    {
        Invoice invoice = Invoice.Create(TestUserId, $"INV-CFS-{Guid.NewGuid():N}", "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, TestUserId, TimeProvider.System);
        invoice.SetCustomFields(new Dictionary<string, object>
        {
            ["region"] = "us-east",
            ["team"] = "backend"
        });

        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        Dictionary<string, string> criteria = new()
        {
            ["region"] = "us-east",
            ["team"] = "backend"
        };
        IReadOnlyList<Invoice> result = await DbContext.Invoices.FindByCustomFieldsAsync(criteria);

        result.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Should().Contain(i => i.Id == invoice.Id);
    }

    [Fact]
    public async Task CustomFieldValueExistsAsync_WhenExists_ReturnsTrue()
    {
        string uniqueValue = Guid.NewGuid().ToString("N");
        Invoice invoice = Invoice.Create(TestUserId, $"INV-CFE-{Guid.NewGuid():N}", "USD", TestUserId, TimeProvider.System);
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, TestUserId, TimeProvider.System);
        invoice.SetCustomFields(new Dictionary<string, object>
        {
            ["externalId"] = uniqueValue
        });

        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync();

        bool exists = await DbContext.Invoices.CustomFieldValueExistsAsync("externalId", uniqueValue);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CustomFieldValueExistsAsync_WhenNotExists_ReturnsFalse()
    {
        bool exists = await DbContext.Invoices.CustomFieldValueExistsAsync("externalId", "nonexistent-value");

        exists.Should().BeFalse();
    }
}
