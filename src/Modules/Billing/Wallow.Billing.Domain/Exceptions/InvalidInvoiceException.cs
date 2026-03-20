#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Exceptions;

public sealed class InvalidInvoiceException : DomainException
{
    public InvalidInvoiceException(string message)
        : base("Billing.InvalidInvoice", message) { }
}
