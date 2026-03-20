#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Exceptions;

public sealed class InvalidPaymentException : DomainException
{
    public InvalidPaymentException(string message)
        : base("Billing.InvalidPayment", message) { }
}
