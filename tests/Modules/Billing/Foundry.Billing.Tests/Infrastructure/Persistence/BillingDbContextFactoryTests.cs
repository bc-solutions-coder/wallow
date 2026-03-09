using Foundry.Billing.Infrastructure.Persistence;

namespace Foundry.Billing.Tests.Infrastructure.Persistence;

public class BillingDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsNonNullContext()
    {
        BillingDbContextFactory factory = new();

        BillingDbContext context = factory.CreateDbContext(Array.Empty<string>());

        context.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_ContextHasInvoicesDbSet()
    {
        BillingDbContextFactory factory = new();

        BillingDbContext context = factory.CreateDbContext(Array.Empty<string>());

        context.Invoices.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_ContextHasPaymentsDbSet()
    {
        BillingDbContextFactory factory = new();

        BillingDbContext context = factory.CreateDbContext(Array.Empty<string>());

        context.Payments.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_ContextHasSubscriptionsDbSet()
    {
        BillingDbContextFactory factory = new();

        BillingDbContext context = factory.CreateDbContext(Array.Empty<string>());

        context.Subscriptions.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_ContextHasMeterDefinitionsDbSet()
    {
        BillingDbContextFactory factory = new();

        BillingDbContext context = factory.CreateDbContext(Array.Empty<string>());

        context.MeterDefinitions.Should().NotBeNull();
    }
}
