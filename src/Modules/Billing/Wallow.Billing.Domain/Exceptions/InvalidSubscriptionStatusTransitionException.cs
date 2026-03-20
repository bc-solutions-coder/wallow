#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Exceptions;

public sealed class InvalidSubscriptionStatusTransitionException(string fromStatus, string toStatus)
    : DomainException("Billing.InvalidSubscriptionStatusTransition",
        $"Cannot transition subscription from '{fromStatus}' to '{toStatus}'");
