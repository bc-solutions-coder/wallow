using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Domain.Identity;

public readonly record struct InvoiceLineItemId(Guid Value) : IStronglyTypedId<InvoiceLineItemId>
{
    public static InvoiceLineItemId Create(Guid value) => new(value);
    public static InvoiceLineItemId New() => new(Guid.NewGuid());
}
