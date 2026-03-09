using Foundry.Billing.Infrastructure.Workflows;

namespace Foundry.Billing.Tests.Infrastructure.Workflows;

public class InvoiceCreatedTriggerTests
{
    [Fact]
    public void ModuleName_ReturnsBilling()
    {
        InvoiceCreatedTrigger trigger = new();

        trigger.ModuleName.Should().Be("Billing");
    }
}
