using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Domain.Identity;

public readonly record struct PaymentId(Guid Value) : IStronglyTypedId<PaymentId>
{
    public static PaymentId Create(Guid value) => new(value);
    public static PaymentId New() => new(Guid.NewGuid());
}
