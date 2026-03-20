using Wallow.Billing.Infrastructure.Workflows;

namespace Wallow.Billing.Tests.Infrastructure.Workflows;

public class InvoiceCreatedTriggerTests
{
    [Fact]
    public void ModuleName_ReturnsBilling()
    {
        InvoiceCreatedTrigger trigger = new();

        trigger.ModuleName.Should().Be("Billing");
    }
}
