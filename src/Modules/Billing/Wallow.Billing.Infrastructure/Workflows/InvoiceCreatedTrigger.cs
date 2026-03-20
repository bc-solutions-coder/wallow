using Elsa.Workflows;
using Wallow.Shared.Infrastructure.Workflows.Workflows;

namespace Wallow.Billing.Infrastructure.Workflows;

/// <summary>
/// Sample workflow activity triggered when an invoice is created.
/// Demonstrates modules owning their own workflow activities using the shared base class.
/// </summary>
public class InvoiceCreatedTrigger : WorkflowActivityBase
{
    public override string ModuleName => "Billing";

    protected override ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        // Placeholder: wire up to InvoiceCreatedDomainEvent handling as needed
        return ValueTask.CompletedTask;
    }
}
