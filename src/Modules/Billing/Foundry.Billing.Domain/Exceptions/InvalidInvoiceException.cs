#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.Exceptions;

public sealed class InvalidInvoiceException : DomainException
{
    public InvalidInvoiceException(string message)
        : base("Billing.InvalidInvoice", message) { }
}
