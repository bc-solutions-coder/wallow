#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.Exceptions;

public sealed class InvalidSubscriptionStatusTransitionException : DomainException
{
    public InvalidSubscriptionStatusTransitionException(string fromStatus, string toStatus)
        : base("Billing.InvalidSubscriptionStatusTransition",
            $"Cannot transition subscription from '{fromStatus}' to '{toStatus}'")
    { }
}
